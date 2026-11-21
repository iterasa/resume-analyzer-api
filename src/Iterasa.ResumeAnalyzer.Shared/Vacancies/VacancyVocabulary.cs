namespace Iterasa.ResumeAnalyzer.Shared.Vacancies;

/// <summary>Modalidad de trabajo de una vacante.</summary>
public enum Modality {

    /// <summary>Híbrida.</summary>
    Hybrid,

    /// <summary>Remota.</summary>
    Remote,

    /// <summary>Presencial.</summary>
    OnSite,

}

/// <summary>Prioridad de una vacante.</summary>
public enum Priority {

    /// <summary>Alta.</summary>
    High,

    /// <summary>Media.</summary>
    Medium,

    /// <summary>Baja.</summary>
    Low,

}

/// <summary>Estado de una vacante.</summary>
public enum VacancyStatus {

    /// <summary>Recibe CV.</summary>
    Open,

    /// <summary>No recibe CV ni avisa a nadie, no adelanta la retención y sigue contando para el límite del plan.</summary>
    Paused,

    /// <summary>Cerrada: empieza la retención.</summary>
    Closed,

}

/// <summary>Motivo del cierre de una vacante.</summary>
public enum CloseReason {

    /// <summary>Se cubrió.</summary>
    Filled,

    /// <summary>Se canceló.</summary>
    Cancelled,

}

/// <summary>Tipo de requisito.</summary>
public enum RequirementKind {

    /// <summary>Obligatorio.</summary>
    Mandatory,

    /// <summary>Deseable.</summary>
    Desirable,

}

/// <summary>Cambio de estado de una vacante sin datos adicionales.</summary>
public enum VacancyTransition {

    /// <summary>Abierta → en pausa.</summary>
    Pause,

    /// <summary>En pausa → abierta.</summary>
    Resume,

    /// <summary>Cerrada → abierta.</summary>
    Reopen,

}