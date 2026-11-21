using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;

namespace Iterasa.ResumeAnalyzer.UnitTests.FieldEncryption;

/// <summary>Anillos en memoria para probar el cifrado sin base de datos ni KEK.</summary>
internal sealed class TestRings : IKeyRingSource {

    private readonly Dictionary<Guid, List<KeyMaterial>> keys = [];

    public int Loads { get; private set; }

    public TestRings WithScope(Guid scopeId, string encKeyId = "", string idxKeyId = "") {
        Add(scopeId, new KeyMaterial(encKeyId is "" ? NewId() : encKeyId, KeyPurpose.Encryption, 1, KeyStatus.Active, RandomNumberGenerator.GetBytes(32)));
        Add(scopeId, new KeyMaterial(idxKeyId is "" ? NewId() : idxKeyId, KeyPurpose.BlindIndex, 1, KeyStatus.Active, RandomNumberGenerator.GetBytes(32)));
        return this;
    }

    public void Add(Guid scopeId, KeyMaterial key) {
        if (!keys.TryGetValue(scopeId, out var list)) {
            keys[scopeId] = list = [];
        }
        list.Add(key);
    }

    /// <summary>Simula una rotación: la activa pasa a solo descifrar y entra otra versión.</summary>
    public KeyMaterial Rotate(Guid scopeId, KeyPurpose purpose) {
        var list = keys[scopeId];
        var active = list.Single(key => key.Purpose == purpose && key.Status == KeyStatus.Active);
        list.Remove(active);
        list.Add(active with { Status = KeyStatus.DecryptOnly });
        var next = new KeyMaterial(NewId(), purpose, active.Version + 1, KeyStatus.Active, RandomNumberGenerator.GetBytes(32));
        list.Add(next);
        return next;
    }

    public Task<KeyRing> LoadAsync(Guid scopeId, CancellationToken cancellationToken) {
        Loads++;
        return keys.TryGetValue(scopeId, out var list)
            ? Task.FromResult(new KeyRing(scopeId, list))
            : Task.FromException<KeyRing>(new InvalidOperationException($"No keys for {scopeId}."));
    }

    public KeyRingCache Cache(TimeProvider? clock = null) => new(this, clock ?? TimeProvider.System);

    public FieldCipher Cipher(KeyRingCache? cache = null) => new(cache ?? Cache(), NullLogger<FieldCipher>.Instance);

    private static string NewId() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4));

}

/// <summary>Reloj que avanza a mano.</summary>
internal sealed class ManualClock(DateTimeOffset start) : TimeProvider {

    private DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan by) => now += by;

}