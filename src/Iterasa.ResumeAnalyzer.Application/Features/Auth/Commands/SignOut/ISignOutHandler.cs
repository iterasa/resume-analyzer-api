using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Auth.Commands.SignOut;

/// <summary>Cierre de sesión.</summary>
public interface ISignOutHandler {
    /// <summary>Ejecuta el caso de uso.</summary>
    Task<Result> Handle(CancellationToken cancellationToken = default);
}