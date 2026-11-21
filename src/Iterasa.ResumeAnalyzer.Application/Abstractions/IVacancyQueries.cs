using Iterasa.ResumeAnalyzer.Shared;
using Iterasa.ResumeAnalyzer.Shared.Vacancies;

namespace Iterasa.ResumeAnalyzer.Application.Abstractions;

/// <summary>Puerto de lectura de vacantes de la empresa de la operación.</summary>
internal interface IVacancyQueries {

    Task<PagedResult<VacancyRow>> ListAsync(VacancyStatus? status, PageQuery page, CancellationToken cancellationToken = default);

    Task<VacancyRow?> FindAsync(Guid id, CancellationToken cancellationToken = default);

}