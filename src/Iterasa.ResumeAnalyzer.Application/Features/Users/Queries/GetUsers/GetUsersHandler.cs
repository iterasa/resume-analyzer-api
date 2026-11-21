using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Application.Auditing;
using Iterasa.ResumeAnalyzer.Shared;
using Microsoft.Extensions.Logging;

namespace Iterasa.ResumeAnalyzer.Application.Features.Users.Queries.GetUsers;

/// <summary>
/// Caso de uso: listado paginado de usuarios.
/// </summary>
internal sealed class GetUsersHandler(
    ICurrentPrincipal currentPrincipal,
    IUserQueries userQueries,
    ILogger<GetUsersHandler> logger) : IGetUsersHandler {

    /// <inheritdoc />
    public async Task<Result<PagedResult<UserSummary>>> Handle(PageQuery page, CancellationToken cancellationToken = default) {
        var users = await userQueries.ListAsync(page, cancellationToken);
        AuditLog.Succeeded(logger, currentPrincipal.UserId, "user.list", "users");
        return Result<PagedResult<UserSummary>>.Success(users);
    }

}