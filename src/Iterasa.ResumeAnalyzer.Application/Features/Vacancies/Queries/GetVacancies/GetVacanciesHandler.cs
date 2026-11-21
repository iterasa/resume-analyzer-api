using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Queries.GetVacancies;

/// <summary>Caso de uso: vacantes de la empresa activa, las más recientes primero.</summary>
internal sealed class GetVacanciesHandler(VacancyReader reader) : IGetVacanciesHandler {

    /// <inheritdoc />
    public async Task<Result<PagedResult<VacancySummary>>> Handle(GetVacanciesQuery query, CancellationToken cancellationToken = default) =>
        Result<PagedResult<VacancySummary>>.Success(await reader.ListAsync(query.Status, query.Page, cancellationToken));

}