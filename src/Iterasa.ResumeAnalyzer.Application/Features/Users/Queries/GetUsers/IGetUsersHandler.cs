using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Users.Queries.GetUsers;

/// <summary>Listado paginado de usuarios.</summary>
public interface IGetUsersHandler {
    /// <summary>Lista una página.</summary>
    Task<Result<PagedResult<UserSummary>>> Handle(PageQuery page, CancellationToken cancellationToken = default);
}