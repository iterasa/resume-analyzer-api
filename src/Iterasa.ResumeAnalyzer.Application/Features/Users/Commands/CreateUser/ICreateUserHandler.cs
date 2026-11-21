using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Users.Commands.CreateUser;

/// <summary>Alta de usuarios.</summary>
public interface ICreateUserHandler {
    /// <summary>Ejecuta el caso de uso.</summary>
    Task<Result<CreateUserResult>> Handle(CreateUserCommand command, CancellationToken cancellationToken = default);
}