namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.UpdateRequirements;

/// <summary>Sustitución de los requisitos de una vacante, con sus pesos.</summary>
/// <param name="VacancyId">Vacante.</param>
/// <param name="Requirements">Requisitos, en orden. Los que no vengan se quitan.</param>
public sealed record UpdateRequirementsCommand(Guid VacancyId, IReadOnlyList<RequirementInput> Requirements);