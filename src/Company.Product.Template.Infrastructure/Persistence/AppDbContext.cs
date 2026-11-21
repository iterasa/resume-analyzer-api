using Company.Product.Template.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Company.Product.Template.Infrastructure.Persistence;

/// <summary>
/// Contexto EF Core de la aplicación. Una tabla por agregado, en snake_case singular.
/// </summary>
internal sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options) {

    private const string SqliteProviderName = "Microsoft.EntityFrameworkCore.Sqlite";
    private const string NpgsqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";

    /// <summary>Tabla <c>user</c>: identidades.</summary>
    public DbSet<User> Users => Set<User>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder) {

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

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