namespace Iterasa.ResumeAnalyzer.Application.Common;

/// <summary>
/// Política de contraseñas alineada con NIST 800-63B: la longitud es el control, sin reglas
/// de composición.
/// </summary>
internal static class PasswordPolicy {

    public const int MinimumLength = 12;

    /// <summary>NIST pide admitir al menos 64; el tope acota el coste del hash.</summary>
    public const int MaximumLength = 256;

    public static bool IsAcceptable(string? password, out string violation) {

        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumLength) {
            violation = $"The password must be at least {MinimumLength} characters long.";
            return false;
        }

        if (password.Length > MaximumLength) {
            violation = $"The password cannot exceed {MaximumLength} characters.";
            return false;
        }

        violation = string.Empty;
        return true;

    }

}

/// <summary>Bloqueo por intentos fallidos de inicio de sesión.</summary>
internal sealed class SignInOptions {

    public int MaxFailedAttempts { get; set; } = 5;

    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);

}