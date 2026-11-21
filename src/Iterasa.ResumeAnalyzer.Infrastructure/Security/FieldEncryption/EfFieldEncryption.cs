using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;

/// <summary>
/// Marca declarativa de un campo cifrado en la configuración de EF.
/// </summary>
internal static class EncryptedPropertyBuilderExtensions {

    /// <summary>Anotación del modelo con el nombre del campo en <see cref="EncryptedFields" />.</summary>
    public const string Annotation = "Iterasa:EncryptedField";

    /// <summary>
    /// Cifra la columna con su campo del inventario. La columna no lleva límite de longitud: el
    /// criptograma mide más que el dato, y el límite se valida sobre el texto en el dominio.
    /// </summary>
    public static PropertyBuilder<string> IsEncrypted(this PropertyBuilder<string> builder, EncryptedField field) =>
        builder.HasAnnotation(Annotation, field.Name);

    /// <summary>Campo cifrado de una propiedad del modelo, si lo es.</summary>
    public static EncryptedField? FindEncryptedField(this IReadOnlyProperty property) =>
        property.FindAnnotation(Annotation)?.Value is string name ? EncryptedFields.Get(name) : null;

}

/// <summary>Cifra al guardar y descifra al leer, también en proyecciones.</summary>
internal sealed class EncryptedStringConverter(IFieldValueProtector protector, EncryptedField field)
    : ValueConverter<string, string>(
        value => protector.Protect(value, field),
        stored => protector.Unprotect(stored, field));

/// <summary>Lo que los conversores necesitan del cifrado.</summary>
internal interface IFieldValueProtector {

    string Protect(string value, EncryptedField field);

    string Unprotect(string stored, EncryptedField field);

}

/// <summary>
/// El cifrado de campos tal como lo usa EF: conversores, índices ciegos e interceptores.
/// Una instancia por aplicación; cada una produce su propio modelo (ver
/// <see cref="FieldEncryptionModelCacheKeyFactory" />).
/// </summary>
internal sealed class EfFieldEncryption : IFieldValueProtector {

    private readonly FieldCipher? cipher;
    private readonly BlindIndexer? indexer;
    private readonly KeyRingCache? rings;
    private readonly ConcurrentDictionary<IEntityType, (IProperty Property, EncryptedField Field)[]> encryptedByEntityType = new();

    public EfFieldEncryption(FieldCipher cipher, BlindIndexer indexer, KeyRingCache rings) {
        this.cipher = cipher;
        this.indexer = indexer;
        this.rings = rings;
        Interceptors = [
            new FieldEncryptionSaveChangesInterceptor(this),
            new FieldEncryptionWarmupInterceptor(this),
            new EncryptedFieldQueryGuard(),
        ];
    }

    private EfFieldEncryption() {
        Interceptors = [];
    }

    /// <summary>
    /// Para las herramientas de EF (<c>dotnet ef</c>): el modelo completo, sin llaves ni
    /// interceptores. Cifrar o descifrar con ella falla.
    /// </summary>
    public static EfFieldEncryption DesignTime { get; } = new();

    public IReadOnlyList<IInterceptor> Interceptors { get; }

    public string Protect(string value, EncryptedField field) =>
        (cipher ?? throw DesignTimeOnly()).Encrypt(value, field, TenantCryptoScope.Resolve(field));

    public string Unprotect(string stored, EncryptedField field) =>
        (cipher ?? throw DesignTimeOnly()).Decrypt(stored, field, TenantCryptoScope.Resolve(field));

    /// <summary>
    /// Completa el modelo: conversor en cada campo marcado y, en los buscables, las columnas
    /// del índice ciego (<c>&lt;columna&gt;_bidx</c>, <c>&lt;columna&gt;_bidx_v</c>) y su índice.
    /// </summary>
    public void ApplyTo(ModelBuilder modelBuilder) {

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList()) {
            foreach (var property in entityType.GetDeclaredProperties().ToList()) {

                if (property.FindEncryptedField() is not { } field) {
                    continue;
                }

                if (property.ClrType != typeof(string)) {
                    throw new InvalidOperationException($"Encrypted field {field.Name} must be a string.");
                }

                if (property.GetMaxLength() is not null) {
                    throw new InvalidOperationException($"Encrypted column {field.Name} must not have a maximum length.");
                }

                property.SetValueConverter(new EncryptedStringConverter(this, field));

                if (field.Search is not { } search) {
                    continue;
                }

                var hash = entityType.AddProperty(field.BlindIndexProperty, typeof(byte[]));
                hash.SetColumnName(field.BlindIndexColumn);
                hash.IsNullable = property.IsNullable;

                var version = entityType.AddProperty(field.BlindIndexVersionProperty, property.IsNullable ? typeof(int?) : typeof(int));
                version.SetColumnName(field.BlindIndexVersionColumn);

                // En una tabla de empresa la unicidad y la búsqueda son por empresa.
                IReadOnlyList<Microsoft.EntityFrameworkCore.Metadata.IMutableProperty> columns = field.Scope == CryptoScopeKind.Tenant
                    ? [entityType.FindProperty("TenantId") ?? throw new InvalidOperationException($"Tenant field {field.Name} needs a TenantId property."), hash]
                    : [hash];

                var index = entityType.AddIndex(columns);
                index.IsUnique = search.Unique;
                index.SetDatabaseName($"ix_{field.Table}_{field.BlindIndexColumn}");

            }
        }

    }

    /// <summary>Carga los anillos que la operación puede necesitar: plataforma y, si hay, la empresa vigente.</summary>
    internal async ValueTask WarmUpAsync(CancellationToken cancellationToken) {
        if (rings is null || KeyStoreAccess.IsLoading) {
            return;
        }
        await rings.GetAsync(CryptoScopes.Platform, cancellationToken);
        if (TenantCryptoScope.Current is { } tenantId) {
            await rings.GetAsync(tenantId, cancellationToken);
        }
    }

    /// <summary>Calcula los índices ciegos de lo que se va a guardar.</summary>
    internal void ComputeBlindIndexes(DbContext context) {

        if (indexer is null || KeyStoreAccess.IsLoading) {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries()) {

            if (entry.State is not (EntityState.Added or EntityState.Modified)) {
                continue;
            }

            foreach (var (property, field) in EncryptedProperties(entry.Metadata)) {

                if (field.Search is null) {
                    continue;
                }

                var current = entry.Property(property.Name);
                if (entry.State == EntityState.Modified && !current.IsModified) {
                    continue;
                }

                SetBlindIndex(entry, field, current.CurrentValue as string);

            }

        }

    }

    private void SetBlindIndex(EntityEntry entry, EncryptedField field, string? value) {
        var hash = entry.Property(field.BlindIndexProperty);
        var version = entry.Property(field.BlindIndexVersionProperty);
        if (value is null) {
            hash.CurrentValue = null;
            version.CurrentValue = null;
            return;
        }
        var index = indexer!.Compute(field, TenantCryptoScope.Resolve(field), value);
        hash.CurrentValue = index.Hash;
        version.CurrentValue = index.KeyVersion;
    }

    private (IProperty Property, EncryptedField Field)[] EncryptedProperties(IEntityType entityType) =>
        encryptedByEntityType.GetOrAdd(entityType, type => type.GetProperties()
            .Select(property => (Property: property, Field: property.FindEncryptedField()))
            .Where(pair => pair.Field is not null)
            .Select(pair => (pair.Property, pair.Field!))
            .ToArray());

    private static InvalidOperationException DesignTimeOnly() =>
        new("This model was built for EF tooling (design time) and cannot encrypt or decrypt.");

}

/// <summary>
/// Antes de guardar: carga los anillos y calcula los índices ciegos. Los conversores de EF son
/// síncronos y no pueden esperar a KMS, así que las llaves tienen que estar ya en memoria.
/// </summary>
internal sealed class FieldEncryptionSaveChangesInterceptor(EfFieldEncryption encryption) : SaveChangesInterceptor {

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) {
        encryption.WarmUpAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
        if (eventData.Context is { } context) {
            encryption.ComputeBlindIndexes(context);
        }
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
        await encryption.WarmUpAsync(cancellationToken);
        if (eventData.Context is { } context) {
            encryption.ComputeBlindIndexes(context);
        }
        return result;
    }

}

/// <summary>Antes de leer: carga los anillos, para que el descifrado al materializar no espere.</summary>
internal sealed class FieldEncryptionWarmupInterceptor(EfFieldEncryption encryption) : DbCommandInterceptor {

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) {
        encryption.WarmUpAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
        await encryption.WarmUpAsync(cancellationToken);
        return result;
    }

}

/// <summary>
/// Rechaza las consultas que filtran, ordenan, agrupan o comparan sobre un campo cifrado: con
/// cifrado aleatorizado el resultado sería vacío o sin sentido. Se busca con su índice ciego.
/// </summary>
internal sealed class EncryptedFieldQueryGuard : IQueryExpressionInterceptor {

    private static readonly HashSet<string> GuardedOperators = new(StringComparer.Ordinal) {
        "Where", "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending", "GroupBy", "Join", "GroupJoin",
        "Any", "All", "Count", "LongCount", "First", "FirstOrDefault", "Single", "SingleOrDefault",
        "Last", "LastOrDefault", "Min", "Max", "MinBy", "MaxBy", "DistinctBy", "SkipWhile", "TakeWhile",
        "ExceptBy", "IntersectBy", "UnionBy", "CountBy", "AggregateBy",
    };

    private readonly ConcurrentDictionary<IModel, HashSet<(Type, string)>> membersByModel = new();

    public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData) {
        if (eventData.Context?.Model is not { } model) {
            return queryExpression;
        }
        var members = membersByModel.GetOrAdd(model, static m => m.GetEntityTypes()
            .SelectMany(type => type.GetProperties())
            .Where(property => property.FindEncryptedField() is not null && property.PropertyInfo is not null)
            .Select(property => (property.PropertyInfo!.DeclaringType!, property.PropertyInfo.Name))
            .ToHashSet());
        if (members.Count > 0) {
            new Visitor(members).Visit(queryExpression);
        }
        return queryExpression;
    }

    private sealed class Visitor(HashSet<(Type, string)> encrypted) : ExpressionVisitor {

        protected override Expression VisitMethodCall(MethodCallExpression node) {
            if ((node.Method.DeclaringType == typeof(Queryable) || node.Method.DeclaringType == typeof(Enumerable))
                && GuardedOperators.Contains(node.Method.Name)) {
                foreach (var argument in node.Arguments.Skip(1)) {
                    if (StripQuotes(argument) is LambdaExpression lambda && new Finder(encrypted).Find(lambda.Body) is { } member) {
                        throw new InvalidOperationException(
                            $"{member.DeclaringType!.Name}.{member.Name} is encrypted: it cannot be used in {node.Method.Name}. Search it with its blind index.");
                    }
                }
            }
            return base.VisitMethodCall(node);
        }

        private static Expression StripQuotes(Expression expression) =>
            expression is UnaryExpression { NodeType: ExpressionType.Quote } quote ? quote.Operand : expression;

    }

    private sealed class Finder(HashSet<(Type, string)> encrypted) : ExpressionVisitor {

        private MemberInfo? found;

        public MemberInfo? Find(Expression expression) {
            Visit(expression);
            return found;
        }

        protected override Expression VisitMember(MemberExpression node) {
            if (found is null && node.Member.DeclaringType is { } type && encrypted.Contains((type, node.Member.Name))) {
                found = node.Member;
            }
            return base.VisitMember(node);
        }

    }

}

/// <summary>
/// Clave de la caché de modelos de EF: incluye la instancia de cifrado. Sin esto, dos
/// aplicaciones en el mismo proceso (las pruebas) compartirían conversores con llaves ajenas.
/// </summary>
internal sealed class FieldEncryptionModelCacheKeyFactory : IModelCacheKeyFactory {

    public object Create(DbContext context, bool designTime) =>
        context is Persistence.AppDbContext app
            ? (context.GetType(), app.FieldEncryption, designTime)
            : (context.GetType(), designTime);

}