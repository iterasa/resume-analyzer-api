using Iterasa.ResumeAnalyzer.Shared.Vacancies;

namespace Iterasa.ResumeAnalyzer.Domain.Vacancies;

/// <summary>Datos editables de una vacante.</summary>
/// <param name="Title">Puesto.</param>
/// <param name="Modality">Modalidad.</param>
/// <param name="OwnerId">Responsable: un miembro activo de la empresa (lo comprueba el caso de uso).</param>
/// <param name="ClosesOn">Fecha prevista de cierre.</param>
internal sealed record VacancyDetails(string Title, Modality Modality, Guid OwnerId, DateOnly ClosesOn);

/// <summary>Requisito tal como llega de quien edita la vacante.</summary>
/// <param name="Id">Requisito existente, o <see langword="null" /> si es nuevo.</param>
/// <param name="Text">Texto.</param>
/// <param name="Kind">Tipo.</param>
/// <param name="Weight">Peso, o <see langword="null" /> para conservar el actual (o el de por defecto si es nuevo).</param>
internal sealed record RequirementDraft(Guid? Id, string Text, RequirementKind Kind, int? Weight = null);

/// <summary>Datos del alta de una vacante.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="TenantId">Empresa.</param>
/// <param name="Details">Datos editables.</param>
/// <param name="Requirements">Requisitos, en orden.</param>
/// <param name="ApplyToken">Token del formulario público: largo e impredecible, nunca el id.</param>
/// <param name="ActorId">Quien la crea.</param>
internal sealed record VacancyCreationArgs(
    Guid Id,
    Guid TenantId,
    VacancyDetails Details,
    IReadOnlyList<RequirementDraft> Requirements,
    string ApplyToken,
    Guid ActorId);