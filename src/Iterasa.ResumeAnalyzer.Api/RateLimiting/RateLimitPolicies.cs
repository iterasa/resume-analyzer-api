namespace Iterasa.ResumeAnalyzer.Api.RateLimiting;

/// <summary>
/// Nombres de las políticas de limitación de peticiones. Cada controller declara
/// explícitamente la suya con <c>[EnableRateLimiting]</c>.
/// </summary>
internal static class RateLimitPolicies {

    /// <summary>
    /// Política de los endpoints de negocio, particionada por cliente autenticado.
    /// </summary>
    public const string PerClient = "per-client";

    /// <summary>
    /// Política reforzada de inicio de sesión y registro, particionada por IP de origen: acota
    /// la fuerza bruta de contraseñas y la enumeración de correos (ADR 0017).
    /// </summary>
    public const string Authentication = "auth";

}