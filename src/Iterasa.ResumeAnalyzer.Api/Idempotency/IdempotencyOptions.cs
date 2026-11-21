namespace Iterasa.ResumeAnalyzer.Api.Idempotency;

/// <summary>
/// Parámetros de las operaciones idempotentes.
/// </summary>
internal sealed class IdempotencyOptions {

    /// <summary>Horas por defecto durante las que una clave reproduce su respuesta.</summary>
    public const int DefaultRetentionHours = 24;

    /// <summary>
    /// Tiempo durante el que una clave conserva y reproduce su respuesta. Pasado, la clave
    /// puede reutilizarse para una operación nueva.
    /// </summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromHours(DefaultRetentionHours);

}