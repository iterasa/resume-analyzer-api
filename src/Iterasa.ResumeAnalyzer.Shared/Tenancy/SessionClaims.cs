namespace Iterasa.ResumeAnalyzer.Shared.Tenancy;

/// <summary>
/// Claims de una sesión. Los lee la autorización (API) y el contexto de la petición
/// (infraestructura); los escribe solo la autenticación por sesión.
/// </summary>
public static class SessionClaims {

    /// <summary>Usuario.</summary>
    public const string UserId = "sub";

    /// <summary>Sesión.</summary>
    public const string SessionId = "sid";

    /// <summary>Empresa activa.</summary>
    public const string TenantId = "tid";

    /// <summary>Rol en la empresa activa.</summary>
    public const string Role = "role";

}