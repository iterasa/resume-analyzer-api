using Iterasa.ResumeAnalyzer.Shared.Vacancies;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.UpdateVacancy;

/// <summary>Edición de una vacante y sus requisitos (los pesos se conservan).</summary>
/// <param name="VacancyId">Vacante.</param>
/// <param name="Title">Puesto.</param>
/// <param name="Modality">Modalidad.</param>
/// <param name="OwnerId">Responsable.</param>
/// <param name="ClosesOn">Fecha prevista de cierre.</param>
/// <param name="Requirements">Requisitos, en orden. Los que no vengan se quitan.</param>
public sealed record UpdateVacancyCommand(Guid VacancyId, string Title, Modality Modality, Guid OwnerId, DateOnly ClosesOn, IReadOnlyList<RequirementInput> Requirements);