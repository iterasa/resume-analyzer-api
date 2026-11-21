using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.CreateVacancy;

/// <summary>Alta de vacantes.</summary>
public interface ICreateVacancyHandler {
    /// <summary>Ejecuta el caso de uso.</summary>
    Task<Result<VacancyDetail>> Handle(CreateVacancyCommand command, CancellationToken cancellationToken = default);
}