using Iterasa.ResumeAnalyzer.Shared.Tenancy;

namespace Iterasa.ResumeAnalyzer.Application.Abstractions;

/// <summary>
/// Valida el token de la cookie de sesión en cada petición (ADR 0017). Sin caché: una sesión
/// revocada, un usuario dado de baja o un cambio de rol valen desde la petición siguiente.
/// </summary>
public interface ISessionAuthenticator {

    /// <summary>
    /// Sesión vigente del token, con el rol actual en su empresa, o nula si no existe, venció, se
    /// revocó o el usuario ya no es miembro activo. Renueva el vencimiento por inactividad.
    /// </summary>
    /// <param name="token">Token de la cookie.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>La sesión, o <see langword="null" />.</returns>
    Task<AuthenticatedSession?> AuthenticateAsync(string token, CancellationToken cancellationToken = default);

}

/// <summary>Sesión vigente.</summary>
/// <param name="SessionId">Sesión.</param>
/// <param name="UserId">Usuario.</param>
/// <param name="TenantId">Empresa activa.</param>
/// <param name="Role">Rol actual en esa empresa.</param>
public sealed record AuthenticatedSession(Guid SessionId, Guid UserId, Guid TenantId, MemberRole Role);