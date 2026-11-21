using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Application.Auditing;
using Iterasa.ResumeAnalyzer.Application.Features.Me;
using Iterasa.ResumeAnalyzer.Shared;
using Microsoft.Extensions.Logging;

namespace Iterasa.ResumeAnalyzer.Application.Features.Auth.Commands.SwitchTenant;

/// <summary>
/// Caso de uso: pasar a otra empresa. La sesión se sustituye por una nueva (otro id), con el
/// mismo vencimiento absoluto: cambiar de empresa no alarga la sesión.
/// </summary>
internal sealed class SwitchTenantHandler(
    ICurrentPrincipal principal,
    ITenantRepository tenants,
    ISessionStore sessions,
    IUnitOfWork unitOfWork,
    SessionComposer composer,
    TimeProvider clock,
    ILogger<SwitchTenantHandler> logger) : ISwitchTenantHandler {

    private const string Action = "auth.switch-tenant";

    /// <inheritdoc />
    public async Task<Result<SignedIn>> Handle(SwitchTenantCommand command, CancellationToken cancellationToken = default) {

        var target = $"tenant:{command.TenantId}";

        // Mismo error exista o no la empresa: no se confirma qué empresas existen.
        if (!await tenants.IsActiveMemberAsync(command.TenantId, principal.UserId, cancellationToken)) {
            AuditLog.Rejected(logger, principal.UserId, Action, target, "not an active member");
            return Result<SignedIn>.Failure(Error.NotFound("Company not found."));
        }

        var now = clock.GetUtcNow();
        var revoked = await sessions.RevokeAsync(principal.SessionId, now, cancellationToken);

        if (revoked is null) {
            return Result<SignedIn>.Failure(Error.Unauthorized("The session is no longer valid."));
        }

        var credential = sessions.Open(principal.UserId, command.TenantId, revoked.Persistent, now, revoked.AbsoluteExpiresAtUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        AuditLog.Succeeded(logger, principal.UserId, Action, target);

        var session = await composer.ComposeAsync(principal.UserId, command.TenantId, cancellationToken);

        return session is null
            ? Result<SignedIn>.Failure(Error.NotFound("Company not found."))
            : Result<SignedIn>.Success(new SignedIn(session, credential));

    }

}