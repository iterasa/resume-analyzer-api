using Iterasa.ResumeAnalyzer.Shared.Vacancies;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies;

/// <summary>Responsable de una vacante.</summary>
/// <param name="Id">Usuario.</param>
/// <param name="Name">Nombre.</param>
public sealed record VacancyOwner(Guid Id, string Name);

/// <summary>Miembro que puede ser responsable de una vacante.</summary>
/// <param name="Id">Usuario.</param>
/// <param name="Name">Nombre.</param>
public sealed record OwnerOption(Guid Id, string Name);

/// <summary>CV recibidos por canal.</summary>
/// <param name="Whatsapp">Por WhatsApp.</param>
/// <param name="Upload">Por carga manual.</param>
/// <param name="Form">Por el formulario público.</param>
public sealed record VacancySources(int Whatsapp, int Upload, int Form);

/// <summary>Requisito de una vacante.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Text">Texto.</param>
/// <param name="Kind">Tipo.</param>
/// <param name="Weight">Peso, de 1 a 5.</param>
/// <param name="Coverage">Fracción de los CV analizados que lo cumple; nula mientras no se haya evaluado.</param>
public sealed record RequirementView(Guid Id, string Text, RequirementKind Kind, int Weight, double? Coverage);

/// <summary>Vacante en un listado.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Title">Puesto.</param>
/// <param name="Area">Área.</param>
/// <param name="Modality">Modalidad.</param>
/// <param name="Priority">Prioridad.</param>
/// <param name="Status">Estado.</param>
/// <param name="CloseReason">Motivo del cierre, si está cerrada.</param>
/// <param name="ClosesOn">Fecha prevista de cierre.</param>
/// <param name="Owner">Responsable.</param>
/// <param name="Received">CV recibidos.</param>
/// <param name="Analyzed">CV analizados.</param>
/// <param name="OpenDays">Días que lleva (o llevó) abierta.</param>
/// <param name="Sources">CV por canal.</param>
public sealed record VacancySummary(
    Guid Id,
    string Title,
    string Area,
    Modality Modality,
    Priority Priority,
    VacancyStatus Status,
    CloseReason? CloseReason,
    DateOnly ClosesOn,
    VacancyOwner Owner,
    int Received,
    int Analyzed,
    int OpenDays,
    VacancySources Sources);

/// <summary>Detalle de una vacante.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Title">Puesto.</param>
/// <param name="Area">Área.</param>
/// <param name="Modality">Modalidad.</param>
/// <param name="Priority">Prioridad.</param>
/// <param name="Status">Estado.</param>
/// <param name="CloseReason">Motivo del cierre, si está cerrada.</param>
/// <param name="ClosesOn">Fecha prevista de cierre.</param>
/// <param name="Owner">Responsable.</param>
/// <param name="Received">CV recibidos.</param>
/// <param name="Analyzed">CV analizados.</param>
/// <param name="OpenDays">Días que lleva (o llevó) abierta.</param>
/// <param name="Sources">CV por canal.</param>
/// <param name="Requirements">Requisitos, en orden.</param>
/// <param name="Unreadable">CV que no se pudieron leer.</param>
/// <param name="ActiveApplications">Postulaciones fuera de Contratado y Descartado.</param>
/// <param name="RetentionDays">Días de retención de la empresa.</param>
/// <param name="DeletesOn">Día en que se borran sus CV: cierre (o cierre previsto) más la retención.</param>
/// <param name="WhatsappLink">Enlace de WhatsApp; nulo mientras la empresa no conecte WhatsApp.</param>
/// <param name="ApplyUrl">Formulario público de postulación; nulo si está cerrada.</param>
public sealed record VacancyDetail(
    Guid Id,
    string Title,
    string Area,
    Modality Modality,
    Priority Priority,
    VacancyStatus Status,
    CloseReason? CloseReason,
    DateOnly ClosesOn,
    VacancyOwner Owner,
    int Received,
    int Analyzed,
    int OpenDays,
    VacancySources Sources,
    IReadOnlyList<RequirementView> Requirements,
    int Unreadable,
    int ActiveApplications,
    int RetentionDays,
    DateOnly DeletesOn,
    string? WhatsappLink,
    string? ApplyUrl);

/// <summary>Requisito tal como lo envía quien crea o edita la vacante.</summary>
/// <param name="Id">Requisito existente, o nulo si es nuevo.</param>
/// <param name="Text">Texto.</param>
/// <param name="Kind">Tipo.</param>
/// <param name="Weight">Peso de 1 a 5, o nulo para conservar el actual (o el de por defecto si es nuevo).</param>
public sealed record RequirementInput(Guid? Id, string Text, RequirementKind Kind, int? Weight);