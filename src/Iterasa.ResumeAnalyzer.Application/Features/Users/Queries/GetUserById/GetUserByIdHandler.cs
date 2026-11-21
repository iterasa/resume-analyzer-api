using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Application.Auditing;
using Iterasa.ResumeAnalyzer.Shared;
using Microsoft.Extensions.Logging;

namespace Iterasa.ResumeAnalyzer.Application.Features.Users.Queries.GetUserById;

/// <summary>
/// Caso de uso: detalle de un usuario.
/// </summary>
internal sealed class GetUserByIdHandler(
    ICurrentPrincipal currentPrincipal,
    IUserQueries userQueries,
    ILogger<GetUserByIdHandler> logger) : IGetUserByIdHandler {

    /// <inheritdoc />
    public async Task<Result<GetUserByIdResult>> Handle(GetUserByIdQuery query, CancellationToken cancellationToken = default) {
        var user = await userQueries.FindDetailByIdAsync(query.UserId, cancellationToken);
        if (user is null) {
            AuditLog.Rejected(logger, currentPrincipal.UserId, "user.read", $"user:{query.UserId}", "user not found");
            return Result<GetUserByIdResult>.Failure(Error.NotFound("User not found."));
        }
        AuditLog.Succeeded(logger, currentPrincipal.UserId, "user.read", $"user:{query.UserId}");
        return Result<GetUserByIdResult>.Success(user);
    }

}