using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.UpdateRequirements;

/// <summary>Edición de requisitos y pesos.</summary>
public interface IUpdateRequirementsHandler {
    /// <summary>Ejecuta el caso de uso.</summary>
    Task<Result<VacancyDetail>> Handle(UpdateRequirementsCommand command, CancellationToken cancellationToken = default);
}