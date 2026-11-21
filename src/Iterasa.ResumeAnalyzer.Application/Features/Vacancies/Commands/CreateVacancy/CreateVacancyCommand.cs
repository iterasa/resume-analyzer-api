using Iterasa.ResumeAnalyzer.Shared.Vacancies;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.CreateVacancy;

/// <summary>Alta de una vacante.</summary>
/// <param name="Title">Puesto.</param>
/// <param name="Modality">Modalidad.</param>
/// <param name="OwnerId">Responsable.</param>
/// <param name="ClosesOn">Fecha prevista de cierre.</param>
/// <param name="Requirements">Requisitos, en orden (sin id).</param>
public sealed record CreateVacancyCommand(string Title, Modality Modality, Guid OwnerId, DateOnly ClosesOn, IReadOnlyList<RequirementInput> Requirements);