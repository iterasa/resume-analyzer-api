using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Me.Queries.GetSession;

/// <summary>Caso de uso: la sesión de quien llama.</summary>
internal sealed class GetSessionHandler(ICurrentPrincipal principal, SessionComposer composer) : IGetSessionHandler {

    /// <inheritdoc />
    public async Task<Result<SessionView>> Handle(CancellationToken cancellationToken = default) {
        var session = await composer.ComposeAsync(principal.UserId, principal.TenantId, cancellationToken);
        return session is null
            ? Result<SessionView>.Failure(Error.Unauthorized("The session is no longer valid."))
            : Result<SessionView>.Success(session);
    }

}