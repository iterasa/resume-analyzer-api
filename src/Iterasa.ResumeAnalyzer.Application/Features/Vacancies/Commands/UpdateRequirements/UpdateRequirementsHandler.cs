using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Application.Auditing;
using Iterasa.ResumeAnalyzer.Shared;
using Microsoft.Extensions.Logging;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.UpdateRequirements;

/// <summary>
/// Caso de uso: cambiar requisitos y pesos. Peso o tipo solo recalculan; un requisito nuevo o
/// con otro texto se vuelve a evaluar.
/// </summary>
internal sealed class UpdateRequirementsHandler(
    ICurrentPrincipal principal,
    IVacancyRepository vacancies,
    IUnitOfWork unitOfWork,
    VacancyReader reader,
    TimeProvider clock,
    ILogger<UpdateRequirementsHandler> logger) : IUpdateRequirementsHandler {

    private const string Action = "vacancy.requirements";

    /// <inheritdoc />
    public async Task<Result<VacancyDetail>> Handle(UpdateRequirementsCommand command, CancellationToken cancellationToken = default) {

        var target = $"vacancy:{command.VacancyId}";
        var vacancy = await vacancies.FindForUpdateAsync(command.VacancyId, cancellationToken);

        if (vacancy is null) {
            AuditLog.Rejected(logger, principal.UserId, Action, target, "not found");
            return Result<VacancyDetail>.Failure(VacancyErrors.NotFound);
        }

        var changed = vacancy.ReplaceRequirements(VacancyErrors.ToDrafts(command.Requirements), principal.UserId, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        AuditLog.Succeeded(logger, principal.UserId, Action, target);
        logger.LogInformation("Vacancy {VacancyId} has {ChangedRequirements} requirements to evaluate again", vacancy.Id, changed.Count);

        return Result<VacancyDetail>.Success((await reader.FindAsync(vacancy.Id, cancellationToken))!);

    }

}