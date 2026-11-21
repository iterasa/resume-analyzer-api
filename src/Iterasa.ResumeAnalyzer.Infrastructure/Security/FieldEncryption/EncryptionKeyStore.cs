using Iterasa.ResumeAnalyzer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Buffers.Text;
using System.Security.Cryptography;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;

/// <summary>
/// Crea DEKs: 256 bits de un CSPRNG, envueltas por la KEK. La DEK en claro se borra de
/// memoria en cuanto se envuelve.
/// </summary>
internal sealed class EncryptionKeyProvisioner(IKeyEncryptionKeyProvider kek, TimeProvider clock) {

    private const int KeySize = 32;
    private const int KeyIdSize = 8;

    /// <summary>
    /// Deja en el contexto las dos llaves de un ámbito nuevo (cifrado e índice), sin guardar:
    /// se confirman en la misma transacción que crea la empresa. Una empresa sin llaves no
    /// puede escribir datos cifrados.
    /// </summary>
    public async Task ProvisionScopeAsync(AppDbContext dbContext, Guid scopeId, CancellationToken cancellationToken) {
        dbContext.EncryptionKeys.Add(await CreateAsync(scopeId, KeyPurpose.Encryption, 1, cancellationToken));
        dbContext.EncryptionKeys.Add(await CreateAsync(scopeId, KeyPurpose.BlindIndex, 1, cancellationToken));
    }

    /// <summary>Crea una llave activa sin añadirla a ningún contexto.</summary>
    public async Task<EncryptionKeyRecord> CreateAsync(Guid scopeId, KeyPurpose purpose, int version, CancellationToken cancellationToken) {
        var dek = RandomNumberGenerator.GetBytes(KeySize);
        try {
            var wrapped = await kek.WrapAsync(dek, new KeyWrapContext(scopeId, purpose, version), cancellationToken);
            return EncryptionKeyRecord.CreateActive(
                scopeId, purpose, version, NewKeyId(), Convert.ToBase64String(wrapped), kek.CurrentKekId, clock.GetUtcNow());
        } finally {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    // base64url no usa «.», el separador del formato enc1.<key_id>.<datos>.
    private static string NewKeyId() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(KeyIdSize));

}

/// <summary>
/// Marca las consultas que hace el propio almacén de llaves, para que los interceptores no
/// intenten cargar llaves mientras se cargan llaves.
/// </summary>
internal static class KeyStoreAccess {

    private static readonly AsyncLocal<bool> loading = new();

    public static bool IsLoading => loading.Value;

    public static IDisposable Enter() {
        var previous = loading.Value;
        loading.Value = true;
        return new Restore(previous);
    }

    private sealed class Restore(bool previous) : IDisposable {
        public void Dispose() => loading.Value = previous;
    }

}

/// <summary>
/// Carga los anillos desde <c>encryption_key</c> y desenvuelve sus DEKs con la KEK.
/// </summary>
/// <remarks>
/// Las llaves de plataforma se crean solas la primera vez (no hay un «alta de la plataforma»).
/// Las de una empresa no: si faltan es un error, porque se crean con la empresa.
/// </remarks>
internal sealed partial class DatabaseKeyRingSource(
    IServiceScopeFactory scopeFactory,
    IKeyEncryptionKeyProvider kek,
    EncryptionKeyProvisioner provisioner,
    ILogger<DatabaseKeyRingSource> logger) : IKeyRingSource {

    public async Task<KeyRing> LoadAsync(Guid scopeId, CancellationToken cancellationToken) {

        using var access = KeyStoreAccess.Enter();
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var records = await LoadRecordsAsync(dbContext, scopeId, cancellationToken);

        if (records.Count == 0 && scopeId == CryptoScopes.Platform) {
            await ProvisionPlatformAsync(dbContext, cancellationToken);
            records = await LoadRecordsAsync(dbContext, scopeId, cancellationToken);
        }

        if (records.Count == 0) {
            throw new InvalidOperationException($"Crypto scope {scopeId} has no encryption keys. Tenant keys are created with the tenant.");
        }

        var keys = new List<KeyMaterial>(records.Count);
        foreach (var record in records) {
            var dek = await kek.UnwrapAsync(
                Convert.FromBase64String(record.WrappedKey),
                record.KekId,
                new KeyWrapContext(record.ScopeId, record.Purpose, record.Version),
                cancellationToken);
            keys.Add(new KeyMaterial(record.KeyId, record.Purpose, record.Version, record.Status, dek));
        }

        return new KeyRing(scopeId, keys);

    }

    private static Task<List<EncryptionKeyRecord>> LoadRecordsAsync(AppDbContext dbContext, Guid scopeId, CancellationToken cancellationToken) =>
        dbContext.EncryptionKeys
            .AsNoTracking()
            .Where(key => key.ScopeId == scopeId && (key.Status == KeyStatus.Active || key.Status == KeyStatus.DecryptOnly))
            .ToListAsync(cancellationToken);

    private async Task ProvisionPlatformAsync(AppDbContext dbContext, CancellationToken cancellationToken) {
        await provisioner.ProvisionScopeAsync(dbContext, CryptoScopes.Platform, cancellationToken);
        try {
            await dbContext.SaveChangesAsync(cancellationToken);
            LogPlatformKeysCreated(logger);
        } catch (DbUpdateException exception) when (IsUniqueViolation(exception)) {
            // Otra instancia las creó a la vez: el índice único parcial dejó pasar solo una.
            dbContext.ChangeTracker.Clear();
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
        // SQLite solo en las pruebas de integración; sin referenciar su paquete desde aquí.
        || exception.InnerException is { } inner
            && inner.GetType().Name == "SqliteException"
            && inner.Message.Contains("UNIQUE", StringComparison.Ordinal);

    [LoggerMessage(EventId = 9100, Level = LogLevel.Information, Message = "Platform encryption keys created")]
    private static partial void LogPlatformKeysCreated(ILogger logger);

}