namespace Iterasa.ResumeAnalyzer.Application.Features.Users.Commands.SetUserStatus;

/// <summary>Alta o baja lógica de un usuario.</summary>
/// <param name="UserId">Usuario afectado.</param>
/// <param name="IsEnabled">Estado objetivo.</param>
public sealed record SetUserStatusCommand(Guid UserId, bool IsEnabled);