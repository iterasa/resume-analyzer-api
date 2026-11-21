namespace Iterasa.ResumeAnalyzer.Shared.Tenancy;

/// <summary>Estado del plan de una empresa.</summary>
public enum PlanStatus {

    /// <summary>Periodo de prueba.</summary>
    Trial,

    /// <summary>Al corriente.</summary>
    Active,

    /// <summary>Con un cobro pendiente.</summary>
    PastDue,

}