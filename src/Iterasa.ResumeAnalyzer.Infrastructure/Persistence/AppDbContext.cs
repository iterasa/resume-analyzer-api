using Iterasa.ResumeAnalyzer.Domain.Common;
using Iterasa.ResumeAnalyzer.Domain.Tenants;
using Iterasa.ResumeAnalyzer.Domain.Users;
using Iterasa.ResumeAnalyzer.Domain.Vacancies;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.DataProtection;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.Sessions;
using Iterasa.ResumeAnalyzer.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Linq.Expressions;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence;

/// <summary>
/// Contexto EF Core de la aplicación. Una tabla por agregado, en snake_case singular.
/// </summary>
/// <remarks>
/// <para>Los campos cifrados se cifran y descifran aquí, sin que el dominio lo note (ADR 0015).</para>
/// <para>Aislamiento entre empresas (ADR 0018): toda consulta sobre una entidad de empresa
/// (<see cref="ITenantOwned" />) solo ve las filas de la empresa de la operación, y sin empresa
/// no ve ninguna. Guardar una en otra empresa falla (<see cref="TenantOwnershipGuard" />). Las
/// pocas lecturas que cruzan empresas (las membresías de un usuario) lo declaran con
/// <c>IgnoreQueryFilters()</c> y su propio filtro.</para>
/// </remarks>
internal sealed class AppDbContext(DbContextOptions<AppDbContext> options, EfFieldEncryption fieldEncryption) : DbContext(options) {

    private const string SqliteProviderName = "Microsoft.EntityFrameworkCore.Sqlite";
    private const string NpgsqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";

    private static readonly TenantOwnershipGuard OwnershipGuard = new();

    /// <summary>Tabla <c>user</c>: personas del equipo.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Tabla <c>tenant</c>: empresas.</summary>
    public DbSet<Tenant> Tenants => Set<Tenant>();

    /// <summary>Tabla <c>membership</c>: quién pertenece a qué empresa y con qué rol.</summary>
    public DbSet<Membership> Memberships => Set<Membership>();

    /// <summary>Tabla <c>vacancy</c>.</summary>
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();

    /// <summary>Tabla <c>vacancy_requirement</c>.</summary>
    public DbSet<Requirement> Requirements => Set<Requirement>();

    /// <summary>Tabla <c>user_session</c>: sesiones del lado del servidor.</summary>
    public DbSet<UserSession> Sessions => Set<UserSession>();

    /// <summary>Tabla <c>idempotency_key</c>: respuestas de operaciones idempotentes.</summary>
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    /// <summary>Tabla <c>data_protection_key</c>: llaves de ASP.NET Data Protection, cifradas.</summary>
    public DbSet<DataProtectionKeyRecord> DataProtectionKeys => Set<DataProtectionKeyRecord>();

    /// <summary>Tabla <c>encryption_key</c>: llaves de datos envueltas, por ámbito.</summary>
    public DbSet<EncryptionKeyRecord> EncryptionKeys => Set<EncryptionKeyRecord>();

    /// <summary>Cifrado de campos con el que se construye este contexto.</summary>
    internal EfFieldEncryption FieldEncryption => fieldEncryption;

    /// <summary>
    /// Empresa de la operación. Los filtros de consulta la leen en cada ejecución (EF la trata
    /// como parámetro), no al construir el modelo.
    /// </summary>
    internal Guid? CurrentTenantId => TenantCryptoScope.Current;

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
        // Aquí y no en el registro: así los llevan también los contextos que las pruebas
        // configuran por su cuenta.
        optionsBuilder
            .AddInterceptors(fieldEncryption.Interceptors)
            .AddInterceptors(OwnershipGuard)
            .ReplaceService<IModelCacheKeyFactory, FieldEncryptionModelCacheKeyFactory>();
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder) {

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        fieldEncryption.ApplyTo(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(type => typeof(ITenantOwned).IsAssignableFrom(type.ClrType))) {
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(TenantFilter(entityType.ClrType));
        }

        // Concurrencia optimista sobre `xmin`, la columna de sistema que PostgreSQL ya
        // actualiza en cada escritura: lo que recomienda Npgsql, sin columna propia ni
        // migración. SQLite no la tiene, así que en pruebas no se aplica.
        if (string.Equals(Database.ProviderName, NpgsqlProviderName, StringComparison.Ordinal)) {
            modelBuilder.Entity<User>().Property<uint>("Version").IsRowVersion();
            modelBuilder.Entity<Tenant>().Property<uint>("Version").IsRowVersion();
            modelBuilder.Entity<Membership>().Property<uint>("Version").IsRowVersion();
            modelBuilder.Entity<Vacancy>().Property<uint>("Version").IsRowVersion();
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

    /// <summary><c>e =&gt; (Guid?)e.TenantId == this.CurrentTenantId</c>, para cualquier entidad de empresa.</summary>
    private LambdaExpression TenantFilter(Type entityType) {
        var entity = Expression.Parameter(entityType, "entity");
        var owner = Expression.Convert(Expression.Property(entity, nameof(ITenantOwned.TenantId)), typeof(Guid?));
        var current = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
        return Expression.Lambda(Expression.Equal(owner, current), entity);
    }

}