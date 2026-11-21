using System.ComponentModel.DataAnnotations;

namespace Iterasa.ResumeAnalyzer.Api.Contracts;

/// <summary>Cuerpo del registro de una empresa.</summary>
/// <param name="CompanyName">Nombre de la empresa.</param>
/// <param name="Country">País: Guatemala, El Salvador, Honduras, Costa Rica o Panamá.</param>
/// <param name="Name">Nombre de quien la registra.</param>
/// <param name="Email">Su correo.</param>
/// <param name="Password">Su contraseña: 12 caracteres o más.</param>
/// <param name="AcceptTerms">Aceptación de los términos. Obligatoria.</param>
public sealed record RegisterRequest(
    [Required] string CompanyName,
    [Required] string Country,
    [Required] string Name,
    [Required][EmailAddress] string Email,
    [Required] string Password,
    bool AcceptTerms) {

    /// <summary>La contraseña no aparece al imprimir la petición.</summary>
    public override string ToString() => $"RegisterRequest {{ CompanyName = {CompanyName}, Country = {Country} }}";

}

/// <summary>Cuerpo del inicio de sesión.</summary>
/// <param name="Email">Correo.</param>
/// <param name="Password">Contraseña.</param>
/// <param name="RememberMe">Si la cookie debe sobrevivir al cierre del navegador (dentro del vencimiento de la sesión).</param>
public sealed record SignInRequest([Required] string Email, [Required] string Password, bool RememberMe) {

    /// <summary>La contraseña no aparece al imprimir la petición.</summary>
    public override string ToString() => $"SignInRequest {{ RememberMe = {RememberMe} }}";

}

/// <summary>Cuerpo del cambio de empresa.</summary>
/// <param name="TenantId">Empresa a la que pasar.</param>
public sealed record SwitchTenantRequest([Required] Guid? TenantId);