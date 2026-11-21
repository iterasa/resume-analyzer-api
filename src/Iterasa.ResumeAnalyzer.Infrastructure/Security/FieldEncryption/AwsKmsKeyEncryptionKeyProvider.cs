using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using System.Security.Cryptography;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;

/// <summary>
/// KEK en AWS KMS: la aplicación le pide envolver y desenvolver, y la llave no sale de KMS.
/// </summary>
/// <remarks>
/// El contexto de cifrado de KMS autentica ámbito, propósito y versión, y queda registrado en
/// CloudTrail en cada llamada. Las credenciales y la región salen de la cadena estándar del
/// SDK (rol de la tarea, <c>AWS_REGION</c>), nunca de esta aplicación.
/// </remarks>
internal sealed class AwsKmsKeyEncryptionKeyProvider(IAmazonKeyManagementService kms, string kmsKeyId) : IKeyEncryptionKeyProvider {

    public string CurrentKekId => kmsKeyId;

    public async Task<byte[]> WrapAsync(ReadOnlyMemory<byte> dek, KeyWrapContext context, CancellationToken cancellationToken) {
        var plaintext = dek.ToArray();
        try {
            using var stream = new MemoryStream(plaintext, writable: false);
            var response = await kms.EncryptAsync(new EncryptRequest {
                KeyId = kmsKeyId,
                Plaintext = stream,
                EncryptionContext = context.ToKmsContext(),
            }, cancellationToken);
            return response.CiphertextBlob.ToArray();
        } finally {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public async Task<byte[]> UnwrapAsync(byte[] wrapped, string kekId, KeyWrapContext context, CancellationToken cancellationToken) {
        using var stream = new MemoryStream(wrapped, writable: false);
        var response = await kms.DecryptAsync(new DecryptRequest {
            // Con la KEK que la envolvió, no con la vigente: tras rotar la KEK, las DEK viejas
            // se siguen leyendo hasta que se vuelven a envolver.
            KeyId = kekId,
            CiphertextBlob = stream,
            EncryptionContext = context.ToKmsContext(),
        }, cancellationToken);
        using var plaintext = response.Plaintext;
        var dek = plaintext.ToArray();
        if (plaintext.TryGetBuffer(out var buffer)) {
            CryptographicOperations.ZeroMemory(buffer.AsSpan());
        }
        return dek;
    }

}