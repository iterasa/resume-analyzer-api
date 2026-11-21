using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Queries.GetVacancies;

/// <summary>Listado de vacantes.</summary>
public interface IGetVacanciesHandler {
    /// <summary>Ejecuta el caso de uso.</summary>
    Task<Result<PagedResult<VacancySummary>>> Handle(GetVacanciesQuery query, CancellationToken cancellationToken = default);
}