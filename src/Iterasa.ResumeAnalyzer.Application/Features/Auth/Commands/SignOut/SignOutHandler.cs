using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Application.Auditing;
using Iterasa.ResumeAnalyzer.Shared;
using Microsoft.Extensions.Logging;

namespace Iterasa.ResumeAnalyzer.Application.Features.Auth.Commands.SignOut;

/// <summary>Caso de uso: cerrar la sesión actual. La revoca en el servidor: la cookie deja de valer al instante.</summary>
internal sealed class SignOutHandler(
    ICurrentPrincipal principal,
    ISessionStore sessions,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<SignOutHandler> logger) : ISignOutHandler {

    /// <inheritdoc />
    public async Task<Result> Handle(CancellationToken cancellationToken = default) {

        await sessions.RevokeAsync(principal.SessionId, clock.GetUtcNow(), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        AuditLog.Succeeded(logger, principal.UserId, "auth.sign-out", $"session:{principal.SessionId}");

        return Result.Success();

    }

}