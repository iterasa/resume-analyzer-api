using Iterasa.ResumeAnalyzer.Application.Features.Auth;

namespace Iterasa.ResumeAnalyzer.Application.Abstractions;

/// <summary>
/// Sesiones del lado del servidor (ADR 0017). Como los repositorios, deja los cambios en la
/// unidad de trabajo: el caso de uso confirma.
/// </summary>
internal interface ISessionStore {

    /// <summary>
    /// Abre una sesión nueva en la empresa indicada y devuelve su token, que solo existe en esa
    /// respuesta. <paramref name="absoluteExpiresAtUtc" /> conserva el vencimiento de la sesión
    /// a la que sustituye: cambiar de empresa no alarga la sesión.
    /// </summary>
    IssuedSession Open(Guid userId, Guid tenantId, bool persistent, DateTimeOffset now, DateTimeOffset? absoluteExpiresAtUtc = null);

    /// <summary>
    /// Revoca una sesión vigente, al instante y de forma atómica: de dos peticiones simultáneas
    /// sobre la misma sesión (cerrar y cambiar de empresa), solo una la revoca. Nulo si no existe,
    /// ya no estaba vigente o la revocó otra.
    /// </summary>
    Task<RevokedSession?> RevokeAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken = default);

}

/// <summary>Sesión recién revocada.</summary>
/// <param name="Persistent">Si su cookie era persistente.</param>
/// <param name="AbsoluteExpiresAtUtc">Su vencimiento absoluto.</param>
internal sealed record RevokedSession(bool Persistent, DateTimeOffset AbsoluteExpiresAtUtc);