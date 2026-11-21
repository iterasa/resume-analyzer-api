using FluentAssertions;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using Xunit;

namespace Iterasa.ResumeAnalyzer.UnitTests.FieldEncryption;

public sealed class BlindIndexerTests {

    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly EncryptedField Email = EncryptedFields.UserEmail;
    private static readonly EncryptedField OtherEmail = new("candidate", "email", CryptoScopeKind.Tenant, new BlindIndexSpec(Normalizers.Email, Unique: false));

    private readonly TestRings rings = new TestRings().WithScope(TenantA).WithScope(TenantB);

    [Fact]
    public void Within_A_Scope_The_Index_Is_Deterministic_And_Normalized() {
        var indexer = new BlindIndexer(rings.Cache());
        var first = indexer.Compute(Email, TenantA, "ana@example.test");
        var second = indexer.Compute(Email, TenantA, "  ANA@Example.TEST ");
        first.Hash.Should().Equal(second.Hash);
        first.Hash.Should().HaveCount(32);
        first.KeyVersion.Should().Be(1);
    }

    [Fact]
    public void Another_Scope_Gives_Another_Index() {
        var indexer = new BlindIndexer(rings.Cache());
        indexer.Compute(Email, TenantA, "ana@example.test").Hash
            .Should().NotEqual(indexer.Compute(Email, TenantB, "ana@example.test").Hash);
    }

    [Fact]
    public void Another_Field_Gives_Another_Index() {
        var indexer = new BlindIndexer(rings.Cache());
        indexer.Compute(Email, TenantA, "ana@example.test").Hash
            .Should().NotEqual(indexer.Compute(OtherEmail, TenantA, "ana@example.test").Hash);
    }

    [Fact]
    public async Task During_An_Index_Rotation_The_Lookup_Covers_Both_Versions() {
        var cache = rings.Cache();
        var indexer = new BlindIndexer(cache);
        var old = indexer.Compute(Email, TenantA, "ana@example.test");

        rings.Rotate(TenantA, KeyPurpose.BlindIndex);
        cache.Invalidate(TenantA);
        var fresh = indexer.Compute(Email, TenantA, "ana@example.test");
        var lookup = await indexer.ComputeForLookupAsync(Email, TenantA, "ana@example.test", CancellationToken.None);

        fresh.KeyVersion.Should().Be(2);
        lookup.Should().HaveCount(2);
        lookup.Should().ContainEquivalentOf(old.Hash).And.ContainEquivalentOf(fresh.Hash);
    }

    /// <summary>Vector fijo calculado con Node.js crypto (HMAC-SHA256).</summary>
    [Fact]
    public void A_Known_Key_Gives_The_Known_Index() {
        var key = Enumerable.Range(0, 32).Select(i => (byte)(0x40 + i)).ToArray();
        Convert.ToHexStringLower(BlindIndexer.Hash(key, Email, "ana@example.test"))
            .Should().Be("bbc5b763d07541207160a5e59847dcad71d037b61467be059267102121cc4fc9");
    }

}

public sealed class NormalizerTests {

    [Theory]
    [InlineData("ana@example.test", "ana@example.test")]
    [InlineData("  Ana@Example.TEST\t", "ana@example.test")]
    [InlineData("ａｎａ@example.test", "ana@example.test")] // ancho completo → NFKC
    public void Email(string input, string expected) =>
        Normalizers.Email.Apply(input).Should().Be(expected);

    [Theory]
    [InlineData("José Pérez", "jose perez")]
    [InlineData("  JOSÉ   PÉREZ  ", "jose perez")]
    [InlineData("Ñandú Müller", "nandu muller")]
    [InlineData("María\tJosé\nLópez", "maria jose lopez")]
    [InlineData("ｊｏｓé", "jose")]
    public void Text(string input, string expected) =>
        Normalizers.Text.Apply(input).Should().Be(expected);

}