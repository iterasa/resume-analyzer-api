namespace Iterasa.ResumeAnalyzer.Application.Features.Users.Commands.CreateUser;

/// <summary>Resultado del alta de un usuario.</summary>
/// <param name="Id">Identificador generado.</param>
/// <param name="Username">Nombre de usuario.</param>
/// <param name="Email">Correo de contacto.</param>
public sealed record CreateUserResult(Guid Id, string Username, string Email);