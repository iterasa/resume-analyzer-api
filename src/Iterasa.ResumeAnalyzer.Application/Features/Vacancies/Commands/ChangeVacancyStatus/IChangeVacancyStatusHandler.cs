using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.ChangeVacancyStatus;

/// <summary>Pausa, reanudación y reapertura de vacantes.</summary>
public interface IChangeVacancyStatusHandler {
    /// <summary>Ejecuta el caso de uso.</summary>
    Task<Result<VacancyDetail>> Handle(ChangeVacancyStatusCommand command, CancellationToken cancellationToken = default);
}