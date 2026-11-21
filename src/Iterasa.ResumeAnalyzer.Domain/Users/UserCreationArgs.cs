namespace Iterasa.ResumeAnalyzer.Domain.Users;

/// <summary>
/// Datos del alta de un usuario (Parameter Object).
/// </summary>
/// <param name="Id">Identificador.</param>
/// <param name="Name">Nombre visible.</param>
/// <param name="Email">Correo.</param>
/// <param name="PasswordHash">Hash ya calculado de su contraseña, o nulo si todavía no la tiene.</param>
/// <param name="ActorId">Quien lo da de alta. <see cref="Guid.Empty" /> si fue él mismo al registrarse.</param>
internal sealed record UserCreationArgs(
    Guid Id,
    string Name,
    string Email,
    string? PasswordHash,
    Guid ActorId);