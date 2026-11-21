using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Auth;

/// <summary>Errores de la autenticación.</summary>
internal static class AuthErrors {

    /// <summary>
    /// Uno solo para cuenta inexistente, sin contraseña, bloqueada, dada de baja, sin empresa o
    /// contraseña incorrecta: distinguirlos permitiría enumerar cuentas.
    /// </summary>
    public static readonly Error InvalidCredentials = new("invalid_credentials", "The email or password is incorrect.", ErrorKind.Unauthorized);

}