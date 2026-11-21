using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Application.Auditing;
using Iterasa.ResumeAnalyzer.Application.Common;
using Iterasa.ResumeAnalyzer.Domain.Vacancies;
using Iterasa.ResumeAnalyzer.Shared;
using Microsoft.Extensions.Logging;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.CreateVacancy;

/// <summary>Caso de uso: crear una vacante abierta en la empresa activa.</summary>
internal sealed class CreateVacancyHandler(
    ICurrentPrincipal principal,
    IVacancyRepository vacancies,
    ITenantRepository tenants,
    IUnitOfWork unitOfWork,
    VacancyReader reader,
    TimeProvider clock,
    ILogger<CreateVacancyHandler> logger) : ICreateVacancyHandler {

    private const string Action = "vacancy.create";

    /// <inheritdoc />
    public async Task<Result<VacancyDetail>> Handle(CreateVacancyCommand command, CancellationToken cancellationToken = default) {

        var vacancy = Vacancy.Create(
            new VacancyCreationArgs(
                Guid.NewGuid(),
                principal.TenantId,
                new VacancyDetails(command.Title, command.Modality, command.OwnerId, command.ClosesOn),
                VacancyErrors.ToDrafts(command.Requirements),
                PublicTokens.New(),
                principal.UserId),
            clock.GetUtcNow());

        if (!await tenants.IsActiveMemberAsync(principal.TenantId, command.OwnerId, cancellationToken)) {
            AuditLog.Rejected(logger, principal.UserId, Action, $"tenant:{principal.TenantId}", "owner is not an active member");
            return Result<VacancyDetail>.Failure(VacancyErrors.OwnerNotMember);
        }

        vacancies.Add(vacancy);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        AuditLog.Succeeded(logger, principal.UserId, Action, $"vacancy:{vacancy.Id}");

        return Result<VacancyDetail>.Success(await reader.FindAsync(vacancy.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Vacancy {vacancy.Id} was saved but cannot be read."));

    }

}