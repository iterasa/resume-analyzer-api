using Company.Product.Template.Shared;

namespace Company.Product.Template.Application.Features.Users.Commands.SetUserStatus;

/// <summary>Alta y baja lógica de usuarios.</summary>
public interface ISetUserStatusHandler {
    /// <summary>Ejecuta el caso de uso.</summary>
    Task<Result> Handle(SetUserStatusCommand command, CancellationToken cancellationToken = default);
}