using Iterasa.ResumeAnalyzer.Domain.Users;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence;

/// <summary>
/// Contexto EF Core de la aplicación. Una tabla por agregado, en snake_case singular.
/// </summary>
/// <remarks>
/// Los campos cifrados se cifran y descifran aquí, sin que el dominio lo note (ADR 0015).
/// </remarks>
internal sealed class AppDbContext(DbContextOptions<AppDbContext> options, EfFieldEncryption fieldEncryption) : DbContext(options) {

    private const string SqliteProviderName = "Microsoft.EntityFrameworkCore.Sqlite";
    private const string NpgsqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";

    /// <summary>Tabla <c>user</c>: identidades.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Tabla <c>encryption_key</c>: llaves de datos envueltas, por ámbito.</summary>
    public DbSet<EncryptionKeyRecord> EncryptionKeys => Set<EncryptionKeyRecord>();

    /// <summary>Cifrado de campos con el que se construye este contexto.</summary>
    internal EfFieldEncryption FieldEncryption => fieldEncryption;

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
        // Aquí y no en el registro: así los llevan también los contextos que las pruebas
        // configuran por su cuenta.
        optionsBuilder
            .AddInterceptors(fieldEncryption.Interceptors)
            .ReplaceService<IModelCacheKeyFactory, FieldEncryptionModelCacheKeyFactory>();
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder) {

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        fieldEncryption.ApplyTo(modelBuilder);

        // Concurrencia optimista sobre `xmin`, la columna de sistema que PostgreSQL ya
        // actualiza en cada escritura: lo que recomienda Npgsql, sin columna propia ni
        // migración. SQLite no la tiene, así que en pruebas no se aplica.
        if (string.Equals(Database.ProviderName, NpgsqlProviderName, StringComparison.Ordinal)) {
            modelBuilder.Entity<User>().Property<uint>("Version").IsRowVersion();
        }

    }

    /// <remarks>
    /// SQLite, que usan las pruebas de integración, no sabe comparar ni ordenar
    /// <see cref="DateTimeOffset" />. La conversión a binario le da ese orden y permite
    /// que filtros y ordenaciones se resuelvan en la base de datos. PostgreSQL no la aplica.
    /// </remarks>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) {
        if (string.Equals(Database.ProviderName, SqliteProviderName, StringComparison.Ordinal)) {
            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
    }

}