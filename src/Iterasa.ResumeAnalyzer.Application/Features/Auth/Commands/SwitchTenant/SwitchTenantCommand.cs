namespace Iterasa.ResumeAnalyzer.Application.Features.Auth.Commands.SwitchTenant;

/// <summary>Cambio de empresa activa.</summary>
/// <param name="TenantId">Empresa a la que pasar.</param>
public sealed record SwitchTenantCommand(Guid TenantId);