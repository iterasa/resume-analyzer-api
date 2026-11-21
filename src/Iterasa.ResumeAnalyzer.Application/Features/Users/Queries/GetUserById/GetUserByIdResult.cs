namespace Iterasa.ResumeAnalyzer.Application.Features.Users.Queries.GetUserById;

/// <summary>Detalle de un usuario.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Username">Nombre de usuario.</param>
/// <param name="Email">Correo de contacto.</param>
/// <param name="IsEnabled">Indica si está habilitado.</param>
/// <param name="CreatedAtUtc">Instante UTC de alta.</param>
/// <param name="UpdatedAtUtc">Instante UTC de la última modificación.</param>
public sealed record GetUserByIdResult(
    Guid Id,
    string Username,
    string Email,
    bool IsEnabled,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);