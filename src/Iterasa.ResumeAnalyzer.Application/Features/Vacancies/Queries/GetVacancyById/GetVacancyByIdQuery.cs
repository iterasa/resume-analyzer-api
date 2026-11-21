namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Queries.GetVacancyById;

/// <summary>Detalle de una vacante.</summary>
/// <param name="VacancyId">Vacante.</param>
public sealed record GetVacancyByIdQuery(Guid VacancyId);