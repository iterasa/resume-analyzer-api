using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Me.Queries.GetSession;

/// <summary>Sesión actual.</summary>
public interface IGetSessionHandler {
    /// <summary>Ejecuta el caso de uso.</summary>
    Task<Result<SessionView>> Handle(CancellationToken cancellationToken = default);
}