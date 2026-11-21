namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.Sessions;

/// <summary>Duración de las sesiones (variables <c>SESSION_*</c>).</summary>
internal sealed class SessionOptions {

    public const int DefaultIdleMinutes = 30;
    public const int DefaultAbsoluteHours = 12;

    /// <summary>Inactividad tras la que vence.</summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(DefaultIdleMinutes);

    /// <summary>Vida máxima desde el inicio de sesión.</summary>
    public TimeSpan AbsoluteLifetime { get; set; } = TimeSpan.FromHours(DefaultAbsoluteHours);

    /// <summary>
    /// Cada cuánto se escribe la renovación por uso: escribir en cada petición sería una
    /// escritura por lectura.
    /// </summary>
    public TimeSpan RenewalInterval { get; set; } = TimeSpan.FromMinutes(1);

}