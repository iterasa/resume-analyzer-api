using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Application.Auditing;
using Iterasa.ResumeAnalyzer.Domain.Vacancies;
using Iterasa.ResumeAnalyzer.Shared;
using Microsoft.Extensions.Logging;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.UpdateVacancy;

/// <summary>
/// Caso de uso: editar una vacante. Un requisito nuevo o con otro texto se vuelve a evaluar
/// (cuando exista el análisis, aquí se encola).
/// </summary>
internal sealed class UpdateVacancyHandler(
    ICurrentPrincipal principal,
    IVacancyRepository vacancies,
    ITenantRepository tenants,
    IUnitOfWork unitOfWork,
    VacancyReader reader,
    TimeProvider clock,
    ILogger<UpdateVacancyHandler> logger) : IUpdateVacancyHandler {

    private const string Action = "vacancy.update";

    /// <inheritdoc />
    public async Task<Result<VacancyDetail>> Handle(UpdateVacancyCommand command, CancellationToken cancellationToken = default) {

        var target = $"vacancy:{command.VacancyId}";
        var vacancy = await vacancies.FindForUpdateAsync(command.VacancyId, cancellationToken);

        if (vacancy is null) {
            AuditLog.Rejected(logger, principal.UserId, Action, target, "not found");
            return Result<VacancyDetail>.Failure(VacancyErrors.NotFound);
        }

        if (command.OwnerId != vacancy.OwnerId && !await tenants.IsActiveMemberAsync(principal.TenantId, command.OwnerId, cancellationToken)) {
            AuditLog.Rejected(logger, principal.UserId, Action, target, "owner is not an active member");
            return Result<VacancyDetail>.Failure(VacancyErrors.OwnerNotMember);
        }

        var changed = vacancy.Update(
            new VacancyDetails(command.Title, command.Modality, command.OwnerId, command.ClosesOn),
            [.. command.Requirements.Select(input => new RequirementDraft(input.Id, input.Text, input.Kind))],
            principal.UserId,
            clock.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);

        AuditLog.Succeeded(logger, principal.UserId, Action, target);
        logger.LogInformation("Vacancy {VacancyId} has {ChangedRequirements} requirements to evaluate again", vacancy.Id, changed.Count);

        return Result<VacancyDetail>.Success((await reader.FindAsync(vacancy.Id, cancellationToken))!);

    }

}