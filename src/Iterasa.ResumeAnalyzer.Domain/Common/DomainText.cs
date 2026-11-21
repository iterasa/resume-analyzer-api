using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Domain.Common;

/// <summary>
/// Validación común de los textos cortos que llegan del usuario: obligatorio, sin espacios
/// alrededor, con tope de longitud y sin caracteres de control (ver <see cref="UnsafeText" />).
/// </summary>
internal static class DomainText {

    /// <summary>Texto obligatorio, ya recortado.</summary>
    /// <exception cref="DomainException">Si falta, es largo o lleva caracteres no admitidos.</exception>
    public static string Required(string? value, string field, int maxLength) {

        if (string.IsNullOrWhiteSpace(value)) {
            throw new DomainException($"{field} is required.");
        }

        return Checked(value.Trim(), field, maxLength);

    }

    /// <summary>Texto opcional: vacío se guarda como <see langword="null" />.</summary>
    /// <exception cref="DomainException">Si es largo o lleva caracteres no admitidos.</exception>
    public static string? Optional(string? value, string field, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : Checked(value.Trim(), field, maxLength);

    private static string Checked(string trimmed, string field, int maxLength) {

        if (trimmed.Length > maxLength) {
            throw new DomainException($"{field} cannot exceed {maxLength} characters.");
        }

        if (UnsafeText.Contains(trimmed)) {
            throw new DomainException($"{field} cannot contain control, format or line separator characters.");
        }

        return trimmed;

    }

}