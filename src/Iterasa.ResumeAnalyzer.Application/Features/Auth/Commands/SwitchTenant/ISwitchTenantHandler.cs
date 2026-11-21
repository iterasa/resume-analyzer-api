using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Auth.Commands.SwitchTenant;

/// <summary>Cambio de empresa activa.</summary>
public interface ISwitchTenantHandler {
    /// <summary>Ejecuta el caso de uso.</summary>
    Task<Result<SignedIn>> Handle(SwitchTenantCommand command, CancellationToken cancellationToken = default);
}