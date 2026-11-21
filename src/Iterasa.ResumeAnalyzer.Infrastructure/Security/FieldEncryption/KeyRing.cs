using System.Collections.Concurrent;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;

/// <summary>Una DEK ya desenvuelta.</summary>
internal sealed record KeyMaterial(string KeyId, KeyPurpose Purpose, int Version, KeyStatus Status, byte[] Key);

/// <summary>
/// Las llaves utilizables de un ámbito: las activas y las que solo descifran. Las retiradas
/// y destruidas no se cargan.
/// </summary>
internal sealed class KeyRing {

    private readonly Dictionary<string, KeyMaterial> byKeyId;

    public KeyRing(Guid scopeId, IEnumerable<KeyMaterial> keys) {

        ScopeId = scopeId;
        byKeyId = keys
            .Where(key => key.Status is KeyStatus.Active or KeyStatus.DecryptOnly)
            .ToDictionary(key => key.KeyId, StringComparer.Ordinal);

        ActiveEncryptionKey = SingleActive(KeyPurpose.Encryption);
        ActiveIndexKey = SingleActive(KeyPurpose.BlindIndex);
        IndexKeysForLookup = byKeyId.Values
            .Where(key => key.Purpose == KeyPurpose.BlindIndex)
            .OrderByDescending(key => key.Version)
            .ToArray();

    }

    /// <summary>Ámbito dueño de todas las llaves del anillo.</summary>
    public Guid ScopeId { get; }

    /// <summary>La que cifra.</summary>
    public KeyMaterial ActiveEncryptionKey { get; }

    /// <summary>La que calcula los índices que se escriben.</summary>
    public KeyMaterial ActiveIndexKey { get; }

    /// <summary>Las que se consultan al buscar: la activa y las que aún se están reindexando.</summary>
    public IReadOnlyList<KeyMaterial> IndexKeysForLookup { get; }

    /// <summary>Busca una llave del ámbito por su id. Una llave de otro ámbito no está aquí.</summary>
    public bool TryGet(string keyId, out KeyMaterial key) => byKeyId.TryGetValue(keyId, out key!);

    private KeyMaterial SingleActive(KeyPurpose purpose) {
        var active = byKeyId.Values.Where(key => key.Purpose == purpose && key.Status == KeyStatus.Active).ToList();
        return active.Count == 1
            ? active[0]
            : throw new InvalidOperationException(
                $"Crypto scope {ScopeId} has {active.Count} active {purpose.ToCode()} keys; it needs exactly one.");
    }

}

/// <summary>Carga el anillo de un ámbito desde su almacén.</summary>
internal interface IKeyRingSource {

    /// <exception cref="InvalidOperationException">Si el ámbito no tiene llaves (una empresa sin provisionar).</exception>
    Task<KeyRing> LoadAsync(Guid scopeId, CancellationToken cancellationToken);

}

/// <summary>
/// Caché en memoria de los anillos: el almacén y la KEK solo se consultan cuando falta o venció
/// (10 minutos). Una sola carga a la vez por ámbito.
/// </summary>
/// <remarks>
/// Las llaves en caché no se borran de memoria al vencer: otro hilo puede estar usándolas, y
/// borrarlas lo haría cifrar con ceros. El modelo de amenazas no cubre un servidor de
/// aplicación comprometido (ADR 0015).
/// </remarks>
internal sealed class KeyRingCache(IKeyRingSource source, TimeProvider clock) {

    public static readonly TimeSpan TimeToLive = TimeSpan.FromMinutes(10);
    public const int MaxScopes = 1024;

    private readonly ConcurrentDictionary<Guid, Entry> entries = new();

    /// <summary>Anillo del ámbito, cargándolo si hace falta.</summary>
    public async ValueTask<KeyRing> GetAsync(Guid scopeId, CancellationToken cancellationToken) {
        var entry = GetOrStartLoading(scopeId);
        try {
            return await entry.Ring.Value.WaitAsync(cancellationToken);
        } catch (Exception) when (entry.Ring.Value.IsFaulted || entry.Ring.Value.IsCanceled) {
            entries.TryRemove(new KeyValuePair<Guid, Entry>(scopeId, entry));
            throw;
        }
    }

    /// <summary>
    /// Versión síncrona para los conversores de EF, que no pueden esperar. Los interceptores
    /// ya cargaron el anillo antes de consultar o guardar, así que normalmente no bloquea.
    /// </summary>
    public KeyRing Get(Guid scopeId) {
        if (entries.TryGetValue(scopeId, out var entry) && IsFresh(entry) && entry.Ring.Value.IsCompletedSuccessfully) {
            return entry.Ring.Value.Result;
        }
        return GetAsync(scopeId, CancellationToken.None).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>Olvida el anillo de un ámbito (tras provisionar o rotar sus llaves).</summary>
    public void Invalidate(Guid scopeId) => entries.TryRemove(scopeId, out _);

    private Entry GetOrStartLoading(Guid scopeId) {

        while (true) {

            if (entries.TryGetValue(scopeId, out var existing) && IsFresh(existing)) {
                return existing;
            }

            // La carga compartida no usa el token de quien la inició: si ese llamador se
            // cancela, los demás que esperan el mismo anillo no deben fallar con él.
            var created = new Entry(
                new Lazy<Task<KeyRing>>(() => source.LoadAsync(scopeId, CancellationToken.None), LazyThreadSafetyMode.ExecutionAndPublication),
                clock.GetUtcNow() + TimeToLive);

            var stored = existing is null
                ? entries.GetOrAdd(scopeId, created)
                : entries.TryUpdate(scopeId, created, existing) ? created : null;

            if (stored is not null) {
                Trim();
                return stored;
            }

        }

    }

    private bool IsFresh(Entry entry) => entry.ExpiresAt > clock.GetUtcNow();

    private void Trim() {
        if (entries.Count <= MaxScopes) {
            return;
        }
        foreach (var stale in entries.Where(pair => !IsFresh(pair.Value)).ToList()) {
            entries.TryRemove(stale);
        }
        foreach (var oldest in entries.OrderBy(pair => pair.Value.ExpiresAt).Take(Math.Max(0, entries.Count - MaxScopes)).ToList()) {
            entries.TryRemove(oldest);
        }
    }

    private sealed record Entry(Lazy<Task<KeyRing>> Ring, DateTimeOffset ExpiresAt);

}