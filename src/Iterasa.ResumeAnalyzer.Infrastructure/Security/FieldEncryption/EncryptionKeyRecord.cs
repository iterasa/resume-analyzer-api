namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;

/// <summary>
/// Fila de <c>encryption_key</c>: una llave de datos (DEK) de un ámbito, envuelta por la llave
/// maestra (KEK). La llave en claro nunca se guarda.
/// </summary>
/// <remarks>
/// No es dominio: es estado de infraestructura y solo lo toca esta librería.
/// </remarks>
internal sealed class EncryptionKeyRecord {

    private EncryptionKeyRecord() {
    }

    /// <summary>Identificador de la fila.</summary>
    public Guid Id { get; private set; }

    /// <summary>Ámbito dueño de la llave: una empresa o <see cref="CryptoScopes.Platform" />.</summary>
    public Guid ScopeId { get; private set; }

    /// <summary>Cifrado o índice ciego.</summary>
    public KeyPurpose Purpose { get; private set; }

    /// <summary>Versión dentro del ámbito y el propósito; empieza en 1.</summary>
    public int Version { get; private set; }

    /// <summary>Identificador corto y único que viaja en cada criptograma (<c>enc1.&lt;key_id&gt;.…</c>).</summary>
    public string KeyId { get; private set; } = string.Empty;

    /// <summary>La DEK envuelta por la KEK, en base64.</summary>
    public string WrappedKey { get; private set; } = string.Empty;

    /// <summary>Qué KEK la envolvió (ARN de KMS o huella de la llave local).</summary>
    public string KekId { get; private set; } = string.Empty;

    /// <summary>Estado en su ciclo de vida.</summary>
    public KeyStatus Status { get; private set; }

    /// <summary>Instante UTC de creación.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Instante UTC del último cambio de estado.</summary>
    public DateTimeOffset StatusChangedAtUtc { get; private set; }

    /// <summary>Crea una llave activa.</summary>
    public static EncryptionKeyRecord CreateActive(
        Guid scopeId, KeyPurpose purpose, int version, string keyId, string wrappedKey, string kekId, DateTimeOffset now) => new() {
            Id = Guid.NewGuid(),
            ScopeId = scopeId,
            Purpose = purpose,
            Version = version,
            KeyId = keyId,
            WrappedKey = wrappedKey,
            KekId = kekId,
            Status = KeyStatus.Active,
            CreatedAtUtc = now,
            StatusChangedAtUtc = now,
        };

    /// <summary>Deja de cifrar con ella: solo descifra lo que aún no se recifró.</summary>
    public void MarkDecryptOnly(DateTimeOffset now) {
        if (Status != KeyStatus.Active) {
            throw new InvalidOperationException($"Only an active key can become decrypt-only (key {KeyId} is {Status}).");
        }
        Status = KeyStatus.DecryptOnly;
        StatusChangedAtUtc = now;
    }

}