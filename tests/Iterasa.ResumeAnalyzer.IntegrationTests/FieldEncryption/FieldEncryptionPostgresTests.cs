using FluentAssertions;
using Iterasa.ResumeAnalyzer.Domain.Users;
using Iterasa.ResumeAnalyzer.Infrastructure.Persistence;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using Iterasa.ResumeAnalyzer.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Security.Cryptography;
using Xunit;

namespace Iterasa.ResumeAnalyzer.IntegrationTests.FieldEncryption;

/// <summary>
/// Cifrado de campos contra PostgreSQL real (ADR 0015).
/// </summary>
public sealed class FieldEncryptionPostgresTests {

    private static readonly DateTimeOffset Now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task The_Stored_Email_Is_Never_The_Plain_Text() {

        await using var database = await PostgresTestDatabase.CreateAsync();
        await AddUserAsync(database.CreateServices(), "ana", "Ana@Example.test");

        var row = (await database.QueryAsync("SELECT email, email_bidx, email_bidx_v FROM \"user\"")).Single();

        var stored = (string)row[0]!;
        stored.Should().StartWith("enc1.").And.NotContain("ana").And.NotContain("example");
        ((byte[])row[1]!).Should().HaveCount(32);
        row[2].Should().Be(1);

    }

    [Fact]
    public async Task Entities_And_Projections_Read_The_Decrypted_Value() {

        await using var database = await PostgresTestDatabase.CreateAsync();
        var services = database.CreateServices();
        var id = await AddUserAsync(services, "ana", "ana@example.test");

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var queries = scope.ServiceProvider.GetRequiredService<UserQueries>();

        (await context.Users.SingleAsync(u => u.Id == id)).Email.Value.Should().Be("ana@example.test");
        (await queries.FindDetailByIdAsync(id))!.Email.Should().Be("ana@example.test");
        (await queries.ListAsync(PageQuery.Create(1, 10))).Items.Single().Email.Should().Be("ana@example.test");

    }

    [Fact]
    public async Task An_Exact_Search_Finds_The_Email_Through_Its_Blind_Index() {

        await using var database = await PostgresTestDatabase.CreateAsync();
        var services = database.CreateServices();
        await AddUserAsync(services, "ana", "ana@example.test");

        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserRepository>();

        (await users.ExistsWithEmailAsync("  ANA@Example.TEST ")).Should().BeTrue();
        (await users.ExistsWithEmailAsync("ana2@example.test")).Should().BeFalse();

    }

    [Fact]
    public async Task The_Blind_Index_Keeps_The_Email_Unique_Without_Seeing_It() {

        await using var database = await PostgresTestDatabase.CreateAsync();
        var services = database.CreateServices();
        await AddUserAsync(services, "ana", "ana@example.test");

        var act = () => AddUserAsync(services, "ana-twin", "ANA@example.test");

        (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>().Which.ConstraintName.Should().Be("ix_user_email_bidx");

    }

    [Fact]
    public async Task Filtering_On_An_Encrypted_Column_Is_Rejected() {

        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var scope = database.CreateServices().CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var filter = () => context.Users.Where(u => u.Email.Value == "ana@example.test").ToListAsync();
        var order = () => context.Users.OrderBy(u => u.Email.Value).ToListAsync();

        (await filter.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*encrypted*blind index*");
        (await order.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*encrypted*");

    }

    [Fact]
    public async Task Plain_Text_Or_A_Tampered_Value_Written_Behind_The_Application_Is_Never_Returned() {

        await using var database = await PostgresTestDatabase.CreateAsync();
        var services = database.CreateServices();
        var id = await AddUserAsync(services, "ana", "ana@example.test");
        var stored = (string)(await database.QueryAsync("SELECT email FROM \"user\"")).Single()[0]!;

        foreach (var forged in new[] { "mallory@example.test", stored[..^4] + (stored[^4] == 'A' ? "B" : "A") + stored[^3..] }) {

            await database.ExecuteAsync($"UPDATE \"user\" SET email = '{forged}' WHERE id = '{id}'");
            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var read = () => context.Users.AsNoTracking().SingleAsync(u => u.Id == id);

            await read.Should().ThrowAsync<FieldDecryptionException>();

        }

    }

    [Fact]
    public async Task Platform_Keys_Are_Created_Once_And_Stored_Only_Wrapped() {

        await using var database = await PostgresTestDatabase.CreateAsync();
        await AddUserAsync(database.CreateServices(), "ana", "ana@example.test");
        // Otra instancia con la misma KEK reutiliza las llaves y lee lo de la primera.
        var second = database.CreateServices();
        await AddUserAsync(second, "bea", "bea@example.test");

        var keys = await database.QueryAsync("SELECT purpose, status, version, wrapped_key, kek_id FROM encryption_key ORDER BY purpose");

        keys.Should().HaveCount(2);
        keys.Select(k => (string)k[0]!).Should().Equal("enc", "idx");
        keys.Should().AllSatisfy(k => {
            k[1].Should().Be("active");
            k[2].Should().Be(1);
            Convert.FromBase64String((string)k[3]!).Should().HaveCount(12 + 32 + 16);
            ((string)k[4]!).Should().StartWith("local:");
        });

        await using var scope = second.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<UserQueries>().ListAsync(PageQuery.Create(1, 10))).Items
            .Select(u => u.Email).Should().BeEquivalentTo("ana@example.test", "bea@example.test");

    }

    [Fact]
    public async Task Another_KEK_Cannot_Read_The_Data() {

        await using var database = await PostgresTestDatabase.CreateAsync();
        var id = await AddUserAsync(database.CreateServices(), "ana", "ana@example.test");

        await using var scope = database.CreateServices(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))).CreateAsyncScope();
        var read = () => scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(u => u.Id == id);

        await read.Should().ThrowAsync<CryptographicException>();

    }

    [Fact]
    public async Task The_Database_Allows_Only_One_Active_Key_Per_Scope_And_Purpose() {

        await using var database = await PostgresTestDatabase.CreateAsync();
        await AddUserAsync(database.CreateServices(), "ana", "ana@example.test");

        var act = () => database.ExecuteAsync($"""
            INSERT INTO encryption_key (id, scope_id, purpose, version, key_id, wrapped_key, kek_id, status, created_at_utc, status_changed_at_utc)
            VALUES ('{Guid.NewGuid()}', '{CryptoScopes.Platform}', 'enc', 2, 'second01', 'x', 'local:x', 'active', now(), now())
            """);

        (await act.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be("ix_encryption_key_one_active");

    }

    [Fact]
    public async Task Rotating_The_Encryption_Key_Keeps_Old_Values_Readable_And_Writes_With_The_New_One() {

        await using var database = await PostgresTestDatabase.CreateAsync();
        var services = database.CreateServices();
        await AddUserAsync(services, "ana", "ana@example.test");

        EncryptionKeyRecord next;
        await using (var scope = services.CreateAsyncScope()) {
            next = await scope.ServiceProvider.GetRequiredService<EncryptionKeyRotation>()
                .RotateAsync(CryptoScopes.Platform, KeyPurpose.Encryption, CancellationToken.None);
        }
        await AddUserAsync(services, "bea", "bea@example.test");

        var stored = (await database.QueryAsync("SELECT username, email FROM \"user\" ORDER BY username"))
            .ToDictionary(row => (string)row[0]!, row => FieldCipher.KeyIdOf((string)row[1]!));
        stored["bea"].Should().Be(next.KeyId);
        stored["ana"].Should().NotBe(next.KeyId);

        var statuses = await database.QueryAsync("SELECT version, status FROM encryption_key WHERE purpose = 'enc' ORDER BY version");
        statuses.Select(s => ((int)s[0]!, (string)s[1]!)).Should().Equal((1, "decrypt_only"), (2, "active"));

        await using var read = services.CreateAsyncScope();
        (await read.ServiceProvider.GetRequiredService<UserQueries>().ListAsync(PageQuery.Create(1, 10))).Items
            .Select(u => u.Email).Should().BeEquivalentTo("ana@example.test", "bea@example.test");

    }

    [Fact]
    public async Task During_An_Index_Rotation_Searches_Find_Rows_Indexed_With_Either_Version() {

        await using var database = await PostgresTestDatabase.CreateAsync();
        var services = database.CreateServices();
        await AddUserAsync(services, "ana", "ana@example.test");

        await using (var scope = services.CreateAsyncScope()) {
            await scope.ServiceProvider.GetRequiredService<EncryptionKeyRotation>()
                .RotateAsync(CryptoScopes.Platform, KeyPurpose.BlindIndex, CancellationToken.None);
        }
        await AddUserAsync(services, "bea", "bea@example.test");

        var versions = await database.QueryAsync("SELECT username, email_bidx_v FROM \"user\" ORDER BY username");
        versions.Select(v => ((string)v[0]!, (int)v[1]!)).Should().Equal(("ana", 1), ("bea", 2));

        await using var search = services.CreateAsyncScope();
        var users = search.ServiceProvider.GetRequiredService<UserRepository>();
        (await users.ExistsWithEmailAsync("ana@example.test")).Should().BeTrue();
        (await users.ExistsWithEmailAsync("bea@example.test")).Should().BeTrue();

    }

    [Fact]
    public async Task Tenant_Keys_Are_Saved_In_The_Callers_Transaction_And_A_Tenant_Without_Keys_Cannot_Encrypt() {

        await using var database = await PostgresTestDatabase.CreateAsync();
        var services = database.CreateServices();
        var tenantId = Guid.NewGuid();
        var rings = services.GetRequiredService<KeyRingCache>();

        var withoutKeys = async () => await rings.GetAsync(tenantId, CancellationToken.None);
        (await withoutKeys.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*no encryption keys*");

        await using (var scope = services.CreateAsyncScope()) {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await services.GetRequiredService<EncryptionKeyProvisioner>().ProvisionScopeAsync(context, tenantId, CancellationToken.None);
            (await database.QueryAsync($"SELECT 1 FROM encryption_key WHERE scope_id = '{tenantId}'")).Should().BeEmpty();
            await context.SaveChangesAsync();
        }

        var ring = await rings.GetAsync(tenantId, CancellationToken.None);
        ring.ActiveEncryptionKey.Version.Should().Be(1);
        ring.ActiveIndexKey.Version.Should().Be(1);

    }

    [Fact]
    public async Task The_Migration_Stops_Instead_Of_Leaving_Plain_Text_Emails() {

        await using var database = await PostgresTestDatabase.CreateAsync("20260912121907_InitialCreate");
        await database.ExecuteAsync("""
            INSERT INTO "user" (id, username, email, created_at_utc, created_by, updated_at_utc, updated_by, is_enabled)
            VALUES ('6b8f0a6e-0000-4000-8000-000000000001', 'legacy', 'legacy@example.test', now(), '00000000-0000-0000-0000-000000000000', now(), '00000000-0000-0000-0000-000000000000', true)
            """);

        var migrate = () => database.MigrateAsync();

        (await migrate.Should().ThrowAsync<PostgresException>()).Which.MessageText.Should().Contain("FieldEncryption");
        (await database.QueryAsync("SELECT email FROM \"user\"")).Single()[0].Should().Be("legacy@example.test");

    }

    private static async Task<Guid> AddUserAsync(IServiceProvider services, string username, string email) {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = User.Create(new UserCreationArgs(Guid.NewGuid(), username, email, Guid.Empty), Now);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

}