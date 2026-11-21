using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Queries.GetOwnerOptions;

/// <summary>Responsables posibles de una vacante.</summary>
public interface IGetOwnerOptionsHandler {
    /// <summary>Ejecuta el caso de uso.</summary>
    Task<Result<IReadOnlyList<OwnerOption>>> Handle(CancellationToken cancellationToken = default);
}