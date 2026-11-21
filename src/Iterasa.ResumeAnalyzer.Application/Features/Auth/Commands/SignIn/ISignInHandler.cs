using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Auth.Commands.SignIn;

/// <summary>Inicio de sesión.</summary>
public interface ISignInHandler {
    /// <summary>Ejecuta el caso de uso.</summary>
    Task<Result<SignedIn>> Handle(SignInCommand command, CancellationToken cancellationToken = default);
}