using Company.Product.Template.Shared;

namespace Company.Product.Template.Application.Features.Users.Commands.CreateUser;

/// <summary>Alta de usuarios.</summary>
public interface ICreateUserHandler {
    /// <summary>Ejecuta el caso de uso.</summary>
    Task<Result<CreateUserResult>> Handle(CreateUserCommand command, CancellationToken cancellationToken = default);
}