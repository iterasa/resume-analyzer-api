using System.Security.Cryptography;
using System.Text;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;

/// <summary>Índice ciego de un valor y versión de la llave que lo calculó.</summary>
internal readonly record struct BlindIndex(byte[] Hash, int KeyVersion);

/// <summary>
/// Índices ciegos para buscar campos cifrados sin descifrarlos: HMAC-SHA256 del valor
/// normalizado con la llave <c>idx</c> del ámbito.
/// </summary>
/// <remarks>
/// Dentro de un ámbito el mismo valor da siempre el mismo índice; en otro ámbito da otro, así
/// que no se puede cruzar entre empresas. La etiqueta del campo va delante del valor, de modo
/// que el mismo valor da índices distintos en campos distintos. Fuga aceptada (ADR 0015): el
/// índice revela qué filas del ámbito comparten valor.
/// </remarks>
internal sealed class BlindIndexer(KeyRingCache rings) {

    /// <summary>Índice que se escribe: con la llave activa.</summary>
    public BlindIndex Compute(EncryptedField field, Guid scopeId, string value) {
        var key = rings.Get(scopeId).ActiveIndexKey;
        return new BlindIndex(Hash(key.Key, field, value), key.Version);
    }

    /// <summary>
    /// Índices con los que se busca: uno por cada llave vigente (la activa y las que aún se
    /// están reindexando), para usarlos en <c>bidx IN (…)</c>.
    /// </summary>
    public async Task<IReadOnlyList<byte[]>> ComputeForLookupAsync(
        EncryptedField field, Guid scopeId, string value, CancellationToken cancellationToken) {
        var ring = await rings.GetAsync(scopeId, cancellationToken);
        return ring.IndexKeysForLookup.Select(key => Hash(key.Key, field, value)).ToArray();
    }

    internal static byte[] Hash(ReadOnlySpan<byte> key, EncryptedField field, string value) {
        var spec = field.Search ?? throw new InvalidOperationException($"Field {field.Name} is not searchable.");
        var label = Encoding.UTF8.GetBytes(field.IndexLabel);
        var normalized = Encoding.UTF8.GetBytes(spec.Normalizer.Apply(value));
        var input = new byte[label.Length + 1 + normalized.Length];
        try {
            label.CopyTo(input, 0);
            normalized.CopyTo(input, label.Length + 1);
            return HMACSHA256.HashData(key, input);
        } finally {
            CryptographicOperations.ZeroMemory(input);
            CryptographicOperations.ZeroMemory(normalized);
        }
    }

}