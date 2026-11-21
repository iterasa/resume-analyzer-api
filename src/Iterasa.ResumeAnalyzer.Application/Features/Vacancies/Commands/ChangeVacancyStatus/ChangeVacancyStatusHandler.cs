using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Application.Auditing;
using Iterasa.ResumeAnalyzer.Shared;
using Iterasa.ResumeAnalyzer.Shared.Vacancies;
using Microsoft.Extensions.Logging;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.ChangeVacancyStatus;

/// <summary>Caso de uso: pausar, reanudar o reabrir una vacante (auditado).</summary>
internal sealed class ChangeVacancyStatusHandler(
    ICurrentPrincipal principal,
    IVacancyRepository vacancies,
    IUnitOfWork unitOfWork,
    VacancyReader reader,
    TimeProvider clock,
    ILogger<ChangeVacancyStatusHandler> logger) : IChangeVacancyStatusHandler {

    /// <inheritdoc />
    public async Task<Result<VacancyDetail>> Handle(ChangeVacancyStatusCommand command, CancellationToken cancellationToken = default) {

        var action = $"vacancy.{command.Transition.ToString().ToLowerInvariant()}";
        var target = $"vacancy:{command.VacancyId}";
        var vacancy = await vacancies.FindForUpdateAsync(command.VacancyId, cancellationToken);

        if (vacancy is null) {
            AuditLog.Rejected(logger, principal.UserId, action, target, "not found");
            return Result<VacancyDetail>.Failure(VacancyErrors.NotFound);
        }

        // Un cambio que no aplica al estado actual choca con él: 409, no un error de validación.
        if (!vacancy.Allows(command.Transition)) {
            AuditLog.Rejected(logger, principal.UserId, action, target, $"not allowed while {vacancy.Status}");
            return Result<VacancyDetail>.Failure(Error.Conflict($"The vacancy cannot {command.Transition.ToString().ToLowerInvariant()} while it is {vacancy.Status.ToString().ToLowerInvariant()}."));
        }

        var now = clock.GetUtcNow();

        // Reabrir con la fecha prevista ya pasada contaría la retención desde el pasado.
        if (command.Transition == VacancyTransition.Reopen && !vacancy.CanReopenUntil(command.ClosesOn ?? vacancy.ClosesOn, now)) {
            AuditLog.Rejected(logger, principal.UserId, action, target, "planned close date has passed");
            return Result<VacancyDetail>.Failure(Error.Conflict("The planned close date has passed: send a new closesOn to reopen the vacancy."));
        }

        vacancy.Apply(command.Transition, principal.UserId, now, command.Transition == VacancyTransition.Reopen ? command.ClosesOn : null);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        AuditLog.Succeeded(logger, principal.UserId, action, target);

        return Result<VacancyDetail>.Success((await reader.FindAsync(vacancy.Id, cancellationToken))!);

    }

}