using Iterasa.ResumeAnalyzer.Infrastructure.Persistence;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Security.Cryptography;

namespace Iterasa.ResumeAnalyzer.IntegrationTests.FieldEncryption;

/// <summary>
/// Una base de datos PostgreSQL propia por prueba, creada vacía y borrada al terminar.
/// </summary>
/// <remarks>
/// El cifrado se prueba contra el motor real, no contra SQLite: tipos, índices parciales y
/// errores de unicidad son los de producción. Sin servidor la prueba falla, no se salta.
/// Basta un PostgreSQL desechable, por ejemplo:
/// <c>docker run --rm -d -p 55432:5432 -e POSTGRES_HOST_AUTH_METHOD=trust postgres:18</c> y
/// <c>TEST_POSTGRES_ADMIN_CONNECTION_STRING=Host=127.0.0.1;Port=55432;Username=postgres</c>.
/// </remarks>
internal sealed class PostgresTestDatabase : IAsyncDisposable {

    public const string AdminConnectionVariable = "TEST_POSTGRES_ADMIN_CONNECTION_STRING";

    private readonly string adminConnectionString;
    private readonly string name;
    private readonly List<ServiceProvider> providers = [];

    private PostgresTestDatabase(string adminConnectionString, string name) {
        this.adminConnectionString = adminConnectionString;
        this.name = name;
        ConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = name, Pooling = false }.ConnectionString;
    }

    public string ConnectionString { get; }

    /// <summary>Crea la base y le aplica las migraciones hasta <paramref name="migration" /> (todas por defecto).</summary>
    public static async Task<PostgresTestDatabase> CreateAsync(string? migration = null) {

        var admin = Environment.GetEnvironmentVariable(AdminConnectionVariable);
        if (string.IsNullOrWhiteSpace(admin)) {
            throw new InvalidOperationException(
                $"{AdminConnectionVariable} is not set: the field encryption tests need a real PostgreSQL (see PostgresTestDatabase).");
        }

        var database = new PostgresTestDatabase(admin, $"criba_it_{Guid.NewGuid():N}");
        await database.ExecuteAdminAsync($"CREATE DATABASE \"{database.name}\"");
        await database.MigrateAsync(migration);
        return database;

    }

    /// <summary>Migra con el contexto de diseño, como en un despliegue: sin llaves ni interceptores.</summary>
    public async Task MigrateAsync(string? migration = null) {
        await using var context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options,
            EfFieldEncryption.DesignTime);
        await context.GetService<IMigrator>().MigrateAsync(migration);
    }

    /// <summary>Una «aplicación»: cifrado de campos y contexto, con la KEK local dada.</summary>
    /// <remarks>
    /// Cada aplicación de prueba tiene su propio registro y su propio cifrado, así que EF crea un
    /// proveedor interno por cada una. En producción hay uno; en la suite pasan de veinte y EF lo
    /// avisa como error, por eso aquí se ignora ese aviso y solo ese.
    /// </remarks>
    public ServiceProvider CreateServices(string? localKek = null) {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddOptions<FieldEncryptionOptions>().Configure(options => {
            options.KekProvider = FieldEncryptionOptions.LocalProvider;
            options.LocalKek = localKek ?? DefaultKek;
        });
        services.AddFieldEncryptionCore();
        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(ConnectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
        services.AddScoped<UserRepository>();
        services.AddScoped<SessionQueries>();
        var provider = services.BuildServiceProvider();
        providers.Add(provider);
        return provider;
    }

    /// <summary>KEK local de esta base. Otra KEK no puede leer sus llaves.</summary>
    public string DefaultKek { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>Consulta SQL directa, sin pasar por la aplicación: lo que vería un DBA.</summary>
    public async Task<List<object?[]>> QueryAsync(string sql) {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<object?[]>();
        while (await reader.ReadAsync()) {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++) {
                row[i] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
            }
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>Escritura SQL directa, sin pasar por la aplicación.</summary>
    public async Task ExecuteAsync(string sql) {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync() {
        foreach (var provider in providers) {
            await provider.DisposeAsync();
        }
        await ExecuteAdminAsync($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
    }

    private async Task ExecuteAdminAsync(string sql) {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

}