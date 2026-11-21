namespace Iterasa.ResumeAnalyzer.Application.Features.Auth.Commands.Register;

/// <summary>Alta de una empresa con su primer administrador.</summary>
/// <param name="CompanyName">Nombre de la empresa.</param>
/// <param name="Country">País.</param>
/// <param name="Name">Nombre de quien la registra.</param>
/// <param name="Email">Su correo.</param>
/// <param name="Password">Su contraseña.</param>
/// <param name="AcceptTerms">Aceptación de los términos.</param>
public sealed record RegisterCommand(string CompanyName, string Country, string Name, string Email, string Password, bool AcceptTerms) {

    /// <summary>La contraseña no aparece al imprimir el comando.</summary>
    public override string ToString() => $"RegisterCommand {{ CompanyName = {CompanyName}, Country = {Country} }}";

}