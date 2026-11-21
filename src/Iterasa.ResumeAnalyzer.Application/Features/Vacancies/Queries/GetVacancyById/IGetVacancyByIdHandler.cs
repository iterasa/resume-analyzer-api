using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Queries.GetVacancyById;

/// <summary>Detalle de una vacante.</summary>
public interface IGetVacancyByIdHandler {
    /// <summary>Ejecuta el caso de uso.</summary>
    Task<Result<VacancyDetail>> Handle(GetVacancyByIdQuery query, CancellationToken cancellationToken = default);
}