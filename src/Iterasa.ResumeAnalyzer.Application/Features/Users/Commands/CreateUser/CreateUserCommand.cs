namespace Iterasa.ResumeAnalyzer.Application.Features.Users.Commands.CreateUser;

/// <summary>Alta de un usuario.</summary>
/// <param name="Username">Nombre de usuario.</param>
/// <param name="Email">Correo de contacto.</param>
public sealed record CreateUserCommand(string Username, string Email);