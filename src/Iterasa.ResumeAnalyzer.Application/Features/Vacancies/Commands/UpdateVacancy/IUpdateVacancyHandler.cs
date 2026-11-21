using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.UpdateVacancy;

/// <summary>Edición de vacantes.</summary>
public interface IUpdateVacancyHandler {
    /// <summary>Ejecuta el caso de uso.</summary>
    Task<Result<VacancyDetail>> Handle(UpdateVacancyCommand command, CancellationToken cancellationToken = default);
}