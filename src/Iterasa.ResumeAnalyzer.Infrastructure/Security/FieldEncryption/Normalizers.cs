using System.Globalization;
using System.Text;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;

/// <summary>
/// Normalización de un tipo de dato antes de calcular su índice ciego. Versionada: cambiarla
/// cambia los índices y obliga a reindexar, como una rotación.
/// </summary>
/// <param name="Name">Tipo de dato.</param>
/// <param name="Version">Versión de la regla.</param>
/// <param name="Apply">La regla.</param>
internal sealed record Normalizer(string Name, int Version, Func<string, string> Apply);

/// <summary>
/// Normalizaciones disponibles, una por tipo de dato.
/// </summary>
internal static class Normalizers {

    /// <summary>Correo: sin espacios alrededor, Unicode NFKC y minúsculas.</summary>
    public static readonly Normalizer Email = new("email", 1, value =>
        value.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant());

    /// <summary>
    /// Texto libre (nombres): NFKC, minúsculas, sin tildes ni diéresis y con un solo espacio
    /// entre palabras. «  José   PÉREZ » y «jose perez» dan lo mismo.
    /// </summary>
    public static readonly Normalizer Text = new("text", 1, value => {
        var folded = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(folded.Length);
        var pendingSpace = false;
        foreach (var character in folded) {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) {
                continue;
            }
            if (char.IsWhiteSpace(character)) {
                pendingSpace = builder.Length > 0;
                continue;
            }
            if (pendingSpace) {
                builder.Append(' ');
                pendingSpace = false;
            }
            builder.Append(character);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    });

}