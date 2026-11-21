namespace Iterasa.ResumeAnalyzer.Application.Features.Auth.Commands.SignIn;

/// <summary>Inicio de sesión con correo y contraseña.</summary>
/// <param name="Email">Correo.</param>
/// <param name="Password">Contraseña.</param>
/// <param name="RememberMe">Si la cookie debe sobrevivir al cierre del navegador.</param>
public sealed record SignInCommand(string Email, string Password, bool RememberMe) {

    /// <summary>La contraseña no aparece al imprimir el comando.</summary>
    public override string ToString() => $"SignInCommand {{ RememberMe = {RememberMe} }}";

}