using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Auth.Commands.Register;

/// <summary>Alta de una empresa.</summary>
public interface IRegisterHandler {
    /// <summary>Ejecuta el caso de uso.</summary>
    Task<Result<SignedIn>> Handle(RegisterCommand command, CancellationToken cancellationToken = default);
}