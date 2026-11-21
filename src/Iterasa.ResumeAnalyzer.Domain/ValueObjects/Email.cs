using Iterasa.ResumeAnalyzer.Domain.Common;
using Iterasa.ResumeAnalyzer.Shared;
using System.Text.RegularExpressions;

namespace Iterasa.ResumeAnalyzer.Domain.ValueObjects;

/// <summary>
/// Correo electrónico normalizado: sin espacios y en minúsculas, para que dos formas de
/// escribir la misma dirección sean el mismo valor y choquen en el índice único.
/// </summary>
internal sealed partial record Email {
    public const int MaxLength = 256;

    private Email(string value) {
        Value = value;
    }

    /// <summary>Dirección normalizada.</summary>
    public string Value { get; }

    /// <summary>
    /// Crea el valor a partir de la entrada del usuario.
    /// </summary>
    /// <exception cref="DomainException">Cuando está vacío, es demasiado largo o no tiene forma de correo.</exception>
    public static Email Create(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new DomainException("Email is required.");
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength) {
            throw new DomainException($"Email cannot exceed {MaxLength} characters.");
        }

        // La expresión solo excluye espacios: sin esto pasarían un NUL (que PostgreSQL
        // rechaza y acabaría en un 500) o un carácter invisible que hace de dos direcciones
        // iguales a la vista dos valores distintos para el índice único.
        if (UnsafeText.Contains(normalized)) {
            throw new DomainException("Email cannot contain control, format or line separator characters.");
        }

        if (!EmailRegex().IsMatch(normalized)) {
            throw new DomainException("Email format is invalid.");
        }

        return new Email(normalized);
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Conversión implícita a su valor normalizado.</summary>
    public static implicit operator string(Email email) => email.Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();
}