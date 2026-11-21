namespace Iterasa.ResumeAnalyzer.Shared;

/// <summary>
/// Catálogo de permisos de una persona dentro de una empresa.
/// </summary>
/// <remarks>
/// Los nombres son los que publica la sesión (<c>GET /me</c>) y los que exige cada endpoint con
/// su política. Los concede el rol de la membresía (<see cref="Tenancy.RolePermissions" />): no
/// se asignan uno a uno. Vive en <c>Shared</c> porque lo necesitan a la vez el dominio y la capa
/// de API, que declara una política por permiso.
/// </remarks>
public static class Permissions {

    /// <summary>Crear, editar, pausar y cerrar vacantes.</summary>
    public const string VacanciesManage = "vacancies.manage";

    /// <summary>Ver el ranking completo de una vacante.</summary>
    public const string ApplicationsViewAll = "applications.viewAll";

    /// <summary>Ver la lista corta (Preseleccionado en adelante).</summary>
    public const string ApplicationsViewShortlist = "applications.viewShortlist";

    /// <summary>Mover candidatos de etapa.</summary>
    public const string ApplicationsMove = "applications.move";

    /// <summary>Revelar los datos personales de un candidato.</summary>
    public const string CandidatesReveal = "candidates.reveal";

    /// <summary>Escribir notas.</summary>
    public const string NotesWrite = "notes.write";

    /// <summary>Editar las plantillas de mensajes.</summary>
    public const string TemplatesManage = "templates.manage";

    /// <summary>Configurar la privacidad y atender los derechos de los candidatos.</summary>
    public const string PrivacyManage = "privacy.manage";

    /// <summary>Gestionar el equipo: invitaciones, roles y evaluación anónima.</summary>
    public const string TeamManage = "team.manage";

    /// <summary>Gestionar el plan y los pagos.</summary>
    public const string BillingManage = "billing.manage";

    /// <summary>Consultar la auditoría.</summary>
    public const string AuditView = "audit.view";

    /// <summary>Consultar los reportes.</summary>
    public const string ReportsView = "reports.view";

    /// <summary>Todos los permisos, en el orden en que los publica la sesión.</summary>
    public static IReadOnlyList<string> All { get; } = [
        VacanciesManage,
        ApplicationsViewAll,
        ApplicationsViewShortlist,
        ApplicationsMove,
        CandidatesReveal,
        NotesWrite,
        TemplatesManage,
        PrivacyManage,
        TeamManage,
        BillingManage,
        AuditView,
        ReportsView,
    ];

}