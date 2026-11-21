namespace Iterasa.ResumeAnalyzer.Application.Abstractions;

/// <summary>
/// Abre la empresa de la operación cuando no la fija la sesión: al registrar una empresa, o en
/// un proceso sin petición. Dentro, las consultas solo ven sus filas y los datos se cifran con
/// sus llaves.
/// </summary>
internal interface ITenantScope {

    /// <summary>Abre el ámbito hasta que se libera el resultado; restaura el anterior.</summary>
    IDisposable Enter(Guid tenantId);

}