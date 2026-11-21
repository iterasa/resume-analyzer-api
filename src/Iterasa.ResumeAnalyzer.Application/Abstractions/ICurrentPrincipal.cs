using Iterasa.ResumeAnalyzer.Shared.Tenancy;

namespace Iterasa.ResumeAnalyzer.Application.Abstractions;

/// <summary>
/// Quién hace la petición en curso: usuario, sesión, empresa activa y rol en ella.
/// </summary>
public interface ICurrentPrincipal {

    /// <summary>Identificador del usuario actual, o <see cref="Guid.Empty" /> si es anónimo.</summary>
    Guid UserId { get; }

    /// <summary>Indica si la petición viene autenticada.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Sesión de la petición, o <see cref="Guid.Empty" /> si es anónima.</summary>
    Guid SessionId { get; }

    /// <summary>Empresa activa de la sesión, o <see cref="Guid.Empty" /> si es anónima.</summary>
    Guid TenantId { get; }

    /// <summary>Rol en la empresa activa, o <see langword="null" /> si es anónima.</summary>
    MemberRole? Role { get; }

}