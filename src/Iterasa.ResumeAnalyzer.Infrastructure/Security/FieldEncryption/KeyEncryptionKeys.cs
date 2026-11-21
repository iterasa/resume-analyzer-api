using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;

/// <summary>
/// Contexto con el que se envuelve una DEK. La KEK lo autentica: una DEK envuelta para un
/// ámbito, propósito o versión no se desenvuelve con otro.
/// </summary>
internal readonly record struct KeyWrapContext(Guid ScopeId, KeyPurpose Purpose, int Version) {

    /// <summary>Como contexto de cifrado de KMS (queda además en CloudTrail).</summary>
    public Dictionary<string, string> ToKmsContext() => new(StringComparer.Ordinal) {
        ["app"] = "criba",
        ["scope"] = ScopeId.ToString("D"),
        ["purpose"] = Purpose.ToCode(),
        ["version"] = Version.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>Como datos asociados de AES-GCM.</summary>
    public byte[] ToAssociatedData() => Encoding.UTF8.GetBytes(
        $"dek1|{ScopeId:D}|{Purpose.ToCode()}|{Version.ToString(CultureInfo.InvariantCulture)}");

}

/// <summary>
/// La llave maestra (KEK). Solo envuelve y desenvuelve DEKs; nunca se exporta.
/// </summary>
internal interface IKeyEncryptionKeyProvider {

    /// <summary>KEK con la que se envuelven las DEKs nuevas.</summary>
    string CurrentKekId { get; }

    /// <summary>Envuelve una DEK con la KEK vigente.</summary>
    Task<byte[]> WrapAsync(ReadOnlyMemory<byte> dek, KeyWrapContext context, CancellationToken cancellationToken);

    /// <summary>Desenvuelve una DEK con la KEK que la envolvió.</summary>
    /// <exception cref="CryptographicException">Si la KEK o el contexto no corresponden.</exception>
    Task<byte[]> UnwrapAsync(byte[] wrapped, string kekId, KeyWrapContext context, CancellationToken cancellationToken);

}

/// <summary>
/// KEK local para desarrollo y pruebas: 32 bytes de una variable de entorno, envolviendo con
/// AES-256-GCM. En producción la aplicación no arranca con ella (ver
/// <see cref="FieldEncryptionOptionsValidator" />).
/// </summary>
internal sealed class LocalKeyEncryptionKeyProvider : IKeyEncryptionKeyProvider {

    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] kek;

    public LocalKeyEncryptionKeyProvider(byte[] kek) {
        if (kek.Length != 32) {
            throw new ArgumentException("The local KEK must be 32 bytes.", nameof(kek));
        }
        this.kek = kek.ToArray();
        // Huella para distinguir KEKs: 4 bytes de su SHA-256 no permiten reconstruirla.
        CurrentKekId = $"local:{Convert.ToHexStringLower(SHA256.HashData(kek).AsSpan(0, 4))}";
    }

    public string CurrentKekId { get; }

    public Task<byte[]> WrapAsync(ReadOnlyMemory<byte> dek, KeyWrapContext context, CancellationToken cancellationToken) {
        var output = new byte[NonceSize + dek.Length + TagSize];
        var nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(kek, TagSize);
        aes.Encrypt(nonce, dek.Span, output.AsSpan(NonceSize, dek.Length), output.AsSpan(NonceSize + dek.Length, TagSize), context.ToAssociatedData());
        return Task.FromResult(output);
    }

    public Task<byte[]> UnwrapAsync(byte[] wrapped, string kekId, KeyWrapContext context, CancellationToken cancellationToken) {
        if (!string.Equals(kekId, CurrentKekId, StringComparison.Ordinal)) {
            throw new CryptographicException($"Key was wrapped with KEK {kekId}, but the local KEK is {CurrentKekId}.");
        }
        if (wrapped.Length <= NonceSize + TagSize) {
            throw new CryptographicException("Wrapped key is too short.");
        }
        var length = wrapped.Length - NonceSize - TagSize;
        var dek = new byte[length];
        using var aes = new AesGcm(kek, TagSize);
        aes.Decrypt(wrapped.AsSpan(0, NonceSize), wrapped.AsSpan(NonceSize, length), wrapped.AsSpan(NonceSize + length, TagSize), dek, context.ToAssociatedData());
        return Task.FromResult(dek);
    }

}