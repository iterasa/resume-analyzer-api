using Iterasa.ResumeAnalyzer.Shared.Vacancies;
using System.ComponentModel.DataAnnotations;

namespace Iterasa.ResumeAnalyzer.Api.Contracts;

/// <summary>Requisito al crear o editar una vacante.</summary>
/// <param name="Id">Requisito existente, o nulo si es nuevo. Al crear, siempre nulo.</param>
/// <param name="Text">Texto. Cambiarlo cuenta como requisito nuevo: se vuelve a evaluar.</param>
/// <param name="Kind">Tipo.</param>
public sealed record RequirementRequest(Guid? Id, [Required] string Text, [Required] RequirementKind? Kind);

/// <summary>Requisito con su peso.</summary>
/// <param name="Id">Requisito existente, o nulo si es nuevo.</param>
/// <param name="Text">Texto. Cambiarlo cuenta como requisito nuevo: se vuelve a evaluar.</param>
/// <param name="Kind">Tipo.</param>
/// <param name="Weight">Peso, de 1 a 5. Cambiarlo solo recalcula.</param>
public sealed record WeightedRequirementRequest(Guid? Id, [Required] string Text, [Required] RequirementKind? Kind, [Required][Range(1, 5)] int? Weight);

/// <summary>Cuerpo del alta de una vacante.</summary>
/// <param name="Title">Puesto.</param>
/// <param name="Modality">Modalidad.</param>
/// <param name="OwnerId">Responsable: un miembro activo de la empresa.</param>
/// <param name="ClosesOn">Fecha prevista de cierre (YYYY-MM-DD).</param>
/// <param name="Requirements">Requisitos, en orden: de 1 a 30.</param>
public sealed record CreateVacancyRequest(
    [Required] string Title,
    [Required] Modality? Modality,
    [Required] Guid? OwnerId,
    [Required] DateOnly? ClosesOn,
    [Required] IReadOnlyList<RequirementRequest>? Requirements);

/// <summary>Cuerpo de la edición de una vacante. Los pesos de los requisitos que se conservan no cambian.</summary>
/// <param name="Title">Puesto.</param>
/// <param name="Modality">Modalidad.</param>
/// <param name="OwnerId">Responsable: un miembro activo de la empresa.</param>
/// <param name="ClosesOn">Fecha prevista de cierre (YYYY-MM-DD).</param>
/// <param name="Requirements">Requisitos, en orden. Los que no vengan se quitan.</param>
public sealed record UpdateVacancyRequest(
    [Required] string Title,
    [Required] Modality? Modality,
    [Required] Guid? OwnerId,
    [Required] DateOnly? ClosesOn,
    [Required] IReadOnlyList<RequirementRequest>? Requirements);

/// <summary>Cuerpo de la edición de requisitos y pesos.</summary>
/// <param name="Requirements">Requisitos, en orden. Los que no vengan se quitan.</param>
public sealed record UpdateRequirementsRequest([Required] IReadOnlyList<WeightedRequirementRequest>? Requirements);

/// <summary>Cuerpo opcional de la reapertura de una vacante.</summary>
/// <param name="ClosesOn">Fecha prevista de cierre nueva (YYYY-MM-DD). Obligatoria si la actual ya pasó.</param>
public sealed record ReopenVacancyRequest(DateOnly? ClosesOn);

/// <summary>Cuerpo del cierre de una vacante.</summary>
/// <param name="Reason">Motivo.</param>
/// <param name="HiredApplicationId">Postulación contratada, si se cubrió con una de la vacante.</param>
/// <param name="NotifyCandidates">Si se avisa a quienes siguen en proceso.</param>
public sealed record CloseVacancyRequest([Required] CloseReason? Reason, Guid? HiredApplicationId, bool NotifyCandidates);