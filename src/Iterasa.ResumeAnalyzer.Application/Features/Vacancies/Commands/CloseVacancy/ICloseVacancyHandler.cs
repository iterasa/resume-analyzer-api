using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.CloseVacancy;

/// <summary>Cierre de vacantes.</summary>
public interface ICloseVacancyHandler {
    /// <summary>Ejecuta el caso de uso.</summary>
    Task<Result<VacancyDetail>> Handle(CloseVacancyCommand command, CancellationToken cancellationToken = default);
}