using Company.Product.Template.Application.Abstractions;
using Company.Product.Template.Application.Auditing;
using Company.Product.Template.Shared;
using Microsoft.Extensions.Logging;

namespace Company.Product.Template.Application.Features.Users.Commands.SetUserStatus;

/// <summary>
/// Caso de uso: alta y baja lógica de un usuario.
/// </summary>
internal sealed class SetUserStatusHandler(
    ICurrentPrincipal currentPrincipal,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<SetUserStatusHandler> logger) : ISetUserStatusHandler {

    /// <inheritdoc />
    public async Task<Result> Handle(SetUserStatusCommand command, CancellationToken cancellationToken = default) {

        var action = command.IsEnabled ? "user.enable" : "user.disable";
        var target = $"user:{command.UserId}";

        var user = await userRepository.FindForUpdateAsync(command.UserId, cancellationToken);

        if (user is null) {
            AuditLog.Rejected(logger, currentPrincipal.UserId, action, target, "user not found");
            return Result.Failure(Error.NotFound("User not found."));
        }

        var now = clock.GetUtcNow();

        if (command.IsEnabled) {
            user.Reinstate(currentPrincipal.UserId, now);
        } else {
            user.Suspend(currentPrincipal.UserId, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        AuditLog.Succeeded(logger, currentPrincipal.UserId, action, target);

        return Result.Success();

    }

}