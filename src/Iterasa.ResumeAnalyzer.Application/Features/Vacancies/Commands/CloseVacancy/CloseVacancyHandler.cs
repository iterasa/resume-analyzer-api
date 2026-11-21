using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Application.Auditing;
using Iterasa.ResumeAnalyzer.Shared;
using Microsoft.Extensions.Logging;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.CloseVacancy;

/// <summary>Caso de uso: cerrar una vacante. Empieza a contar la retención (auditado).</summary>
/// <remarks>
/// Mientras no existan las postulaciones, no hay a quién contratar ni avisar: una postulación
/// contratada responde 404 y el aviso queda solo en la auditoría.
/// </remarks>
internal sealed class CloseVacancyHandler(
    ICurrentPrincipal principal,
    IVacancyRepository vacancies,
    IUnitOfWork unitOfWork,
    VacancyReader reader,
    TimeProvider clock,
    ILogger<CloseVacancyHandler> logger) : ICloseVacancyHandler {

    private const string Action = "vacancy.close";

    /// <inheritdoc />
    public async Task<Result<VacancyDetail>> Handle(CloseVacancyCommand command, CancellationToken cancellationToken = default) {

        var target = $"vacancy:{command.VacancyId}";
        var vacancy = await vacancies.FindForUpdateAsync(command.VacancyId, cancellationToken);

        if (vacancy is null) {
            AuditLog.Rejected(logger, principal.UserId, Action, target, "not found");
            return Result<VacancyDetail>.Failure(VacancyErrors.NotFound);
        }

        if (!vacancy.CanClose) {
            AuditLog.Rejected(logger, principal.UserId, Action, target, "already closed");
            return Result<VacancyDetail>.Failure(Error.Conflict("The vacancy is already closed."));
        }

        if (command.HiredApplicationId is not null) {
            AuditLog.Rejected(logger, principal.UserId, Action, target, "hired application not found");
            return Result<VacancyDetail>.Failure(Error.NotFound("Application not found."));
        }

        vacancy.Close(command.Reason, principal.UserId, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        AuditLog.Succeeded(logger, principal.UserId, Action, $"{target} ({command.Reason}, notify: {command.NotifyCandidates})");

        return Result<VacancyDetail>.Success((await reader.FindAsync(vacancy.Id, cancellationToken))!);

    }

}