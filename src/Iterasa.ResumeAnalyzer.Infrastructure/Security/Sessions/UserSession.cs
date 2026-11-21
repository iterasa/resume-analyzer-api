namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.Sessions;

/// <summary>
/// Fila de <c>user_session</c>: una sesión del lado del servidor. Guarda el hash del token, nunca
/// el token: quien lea la base no puede usar una sesión.
/// </summary>
internal sealed class UserSession {

    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Empresa activa. Cambiar de empresa abre otra sesión.</summary>
    public Guid TenantId { get; set; }

    /// <summary>SHA-256 del token de la cookie.</summary>
    public byte[] TokenHash { get; set; } = [];

    /// <summary>Cookie persistente («Recordarme»).</summary>
    public bool Persistent { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }

    /// <summary>Vence si no se usa antes de este instante. Se desliza con el uso.</summary>
    public DateTimeOffset IdleExpiresAtUtc { get; set; }

    /// <summary>Vence en este instante aunque se use.</summary>
    public DateTimeOffset AbsoluteExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

}