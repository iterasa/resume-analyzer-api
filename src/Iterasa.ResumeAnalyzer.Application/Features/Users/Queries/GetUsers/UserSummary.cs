namespace Iterasa.ResumeAnalyzer.Application.Features.Users.Queries.GetUsers;

/// <summary>Usuario en el listado de administración.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Username">Nombre de usuario.</param>
/// <param name="Email">Correo de contacto.</param>
/// <param name="IsEnabled">Indica si está habilitado.</param>
/// <param name="CreatedAtUtc">Instante UTC de alta.</param>
public sealed record UserSummary(
    Guid Id,
    string Username,
    string Email,
    bool IsEnabled,
    DateTimeOffset CreatedAtUtc);