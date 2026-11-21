using Microsoft.Extensions.Logging;
using System.Buffers.Text;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;

/// <summary>
/// Cifra y descifra el valor de un campo con AES-256-GCM y la llave de su ámbito.
/// </summary>
/// <remarks>
/// <para>Formato: <c>enc1.&lt;key_id&gt;.&lt;base64url(nonce ‖ criptograma ‖ tag)&gt;</c>, con nonce
/// aleatorio de 96 bits y tag de 128. El mismo texto da un criptograma distinto cada vez.
/// Un esquema nuevo lleva un prefijo nuevo; nunca se reutiliza uno.</para>
/// <para>Datos asociados: <c>enc1|ámbito|tabla|columna</c>. Un criptograma copiado a otra
/// columna o a otra empresa no descifra.</para>
/// <para>Límite de NIST SP 800-38D con nonce aleatorio: 2^32 cifrados por llave. Con una llave
/// por empresa y rotación periódica no se alcanza.</para>
/// </remarks>
internal sealed partial class FieldCipher(KeyRingCache rings, ILogger<FieldCipher> logger) {

    public const string Prefix = "enc1.";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    /// <summary>Cifra con la llave activa del ámbito.</summary>
    public string Encrypt(string plaintext, EncryptedField field, Guid scopeId) {
        Span<byte> nonce = stackalloc byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);
        return EncryptWith(plaintext, field, scopeId, rings.Get(scopeId).ActiveEncryptionKey, nonce);
    }

    /// <summary>Cifrado con llave y nonce dados: solo para los vectores fijos de las pruebas.</summary>
    internal static string EncryptWith(string plaintext, EncryptedField field, Guid scopeId, KeyMaterial key, ReadOnlySpan<byte> nonce) {

        var plain = Encoding.UTF8.GetBytes(plaintext);
        try {
            var payload = new byte[NonceSize + plain.Length + TagSize];
            nonce.CopyTo(payload);
            using var aes = new AesGcm(key.Key, TagSize);
            aes.Encrypt(nonce, plain, payload.AsSpan(NonceSize, plain.Length), payload.AsSpan(NonceSize + plain.Length, TagSize), AssociatedData(field, scopeId));
            return $"{Prefix}{key.KeyId}.{Base64Url.EncodeToString(payload)}";
        } finally {
            CryptographicOperations.ZeroMemory(plain);
        }

    }

    /// <summary>
    /// Descifra comprobando que la llave sea del ámbito esperado y que el tag valide.
    /// Nunca devuelve el criptograma como si fuera el dato, ni acepta texto en claro.
    /// </summary>
    /// <exception cref="FieldDecryptionException">Formato, llave o tag inválidos.</exception>
    public string Decrypt(string stored, EncryptedField field, Guid scopeId) {

        if (!TryParse(stored, out var keyId, out var payload)) {
            throw Fail(field, scopeId, keyId: null, "format");
        }

        // El anillo es solo del ámbito esperado: una llave de otra empresa no está en él.
        if (!rings.Get(scopeId).TryGet(keyId, out var key) || key.Purpose != KeyPurpose.Encryption) {
            throw Fail(field, scopeId, keyId, "unknown_key");
        }

        var length = payload.Length - NonceSize - TagSize;
        var plain = new byte[length];
        try {
            using var aes = new AesGcm(key.Key, TagSize);
            aes.Decrypt(payload.AsSpan(0, NonceSize), payload.AsSpan(NonceSize, length), payload.AsSpan(NonceSize + length, TagSize), plain, AssociatedData(field, scopeId));
            return Encoding.UTF8.GetString(plain);
        } catch (AuthenticationTagMismatchException) {
            throw Fail(field, scopeId, keyId, "tag");
        } finally {
            CryptographicOperations.ZeroMemory(plain);
        }

    }

    /// <summary>Si un valor almacenado tiene el formato vigente (sin descifrarlo).</summary>
    public static bool IsCurrentFormat(string? stored) => stored is not null && TryParse(stored, out _, out _);

    /// <summary>Id de la llave con la que se cifró un valor almacenado.</summary>
    public static string? KeyIdOf(string stored) => TryParse(stored, out var keyId, out _) ? keyId : null;

    private static bool TryParse(string stored, out string keyId, out byte[] payload) {

        keyId = string.Empty;
        payload = [];

        if (!stored.StartsWith(Prefix, StringComparison.Ordinal)) {
            return false;
        }

        var separator = stored.IndexOf('.', Prefix.Length);
        if (separator <= Prefix.Length) {
            return false;
        }

        keyId = stored[Prefix.Length..separator];
        var encoded = stored.AsSpan(separator + 1);
        if (!Base64Url.IsValid(encoded, out var decodedLength) || decodedLength < NonceSize + TagSize) {
            return false;
        }

        payload = new byte[decodedLength];
        return Base64Url.TryDecodeFromChars(encoded, payload, out var written) && written == decodedLength;

    }

    private static byte[] AssociatedData(EncryptedField field, Guid scopeId) =>
        Encoding.UTF8.GetBytes($"enc1|{scopeId:D}|{field.Table}|{field.Column}");

    private FieldDecryptionException Fail(EncryptedField field, Guid scopeId, string? keyId, string reason) {
        FieldEncryptionTelemetry.DecryptFailures.Add(1,
            new KeyValuePair<string, object?>("field", field.Name),
            new KeyValuePair<string, object?>("reason", reason));
        LogDecryptFailed(logger, field.Name, scopeId, keyId ?? "-", reason);
        return new FieldDecryptionException(field.Name, reason);
    }

    // Evento de seguridad: qué campo, qué ámbito, qué llave y por qué. Nunca el valor.
    [LoggerMessage(EventId = 9001, Level = LogLevel.Warning,
        Message = "Security: could not decrypt {Field} in scope {ScopeId} with key {KeyId} ({Reason})")]
    private static partial void LogDecryptFailed(ILogger logger, string field, Guid scopeId, string keyId, string reason);

}

/// <summary>
/// Un valor cifrado no se pudo descifrar: formato desconocido, llave que no es del ámbito o
/// tag que no valida. El mensaje nunca lleva el valor, ni en claro ni cifrado.
/// </summary>
internal sealed class FieldDecryptionException(string field, string reason)
    : Exception($"Could not decrypt {field} ({reason}).") {

    /// <summary>Campo afectado (<c>tabla.columna</c>).</summary>
    public string Field { get; } = field;

    /// <summary><c>format</c>, <c>unknown_key</c> o <c>tag</c>.</summary>
    public string Reason { get; } = reason;

}

/// <summary>Métricas del cifrado de campos.</summary>
internal static class FieldEncryptionTelemetry {

    public static readonly Meter Meter = new("Iterasa.ResumeAnalyzer.FieldEncryption");

    /// <summary>Descifrados fallidos, por campo y motivo. Cualquier valor distinto de cero se investiga.</summary>
    public static readonly Counter<long> DecryptFailures = Meter.CreateCounter<long>("field_encryption.decrypt_failures");

}