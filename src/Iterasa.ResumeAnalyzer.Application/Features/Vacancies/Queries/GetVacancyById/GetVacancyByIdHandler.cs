using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Queries.GetVacancyById;

/// <summary>Caso de uso: detalle de una vacante de la empresa activa.</summary>
internal sealed class GetVacancyByIdHandler(VacancyReader reader) : IGetVacancyByIdHandler {

    /// <inheritdoc />
    public async Task<Result<VacancyDetail>> Handle(GetVacancyByIdQuery query, CancellationToken cancellationToken = default) {
        var detail = await reader.FindAsync(query.VacancyId, cancellationToken);
        return detail is null
            ? Result<VacancyDetail>.Failure(VacancyErrors.NotFound)
            : Result<VacancyDetail>.Success(detail);
    }

}