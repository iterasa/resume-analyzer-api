namespace Iterasa.ResumeAnalyzer.Api.RateLimiting;

/// <summary>
/// Opciones del limitador de peticiones, alimentadas desde variables de entorno.
/// </summary>
internal sealed class RateLimitingOptions {

    /// <summary>
    /// Activa o desactiva el limitador por completo.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Peticiones permitidas por ventana en los endpoints de negocio.
    /// </summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>
    /// Duración de la ventana de los endpoints de negocio, en segundos.
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Peticiones permitidas por ventana en inicio de sesión y registro.
    /// </summary>
    public int AuthPermitLimit { get; set; } = 10;

    /// <summary>
    /// Duración de la ventana de inicio de sesión y registro, en segundos.
    /// </summary>
    public int AuthWindowSeconds { get; set; } = 60;

}