using Company.Product.Template.Application.Abstractions;
using Company.Product.Template.Application.Auditing;
using Company.Product.Template.Domain.Users;
using Company.Product.Template.Shared;
using Microsoft.Extensions.Logging;

namespace Company.Product.Template.Application.Features.Users.Commands.CreateUser;

/// <summary>
/// Caso de uso: dar de alta un usuario.
/// </summary>
internal sealed class CreateUserHandler(
    ICurrentPrincipal currentPrincipal,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<CreateUserHandler> logger) : ICreateUserHandler {

    /// <remarks>Mensaje genérico: confirmar qué campo choca filtraría el padrón.</remarks>
    private static readonly Error AlreadyExists =
        Error.Conflict("The user could not be registered because it conflicts with an existing one.");

    private const string Action = "user.create";

    /// <inheritdoc />
    public async Task<Result<CreateUserResult>> Handle(CreateUserCommand command, CancellationToken cancellationToken = default) {

        var actorId = currentPrincipal.IsAuthenticated ? currentPrincipal.UserId : Guid.Empty;

        // El dominio valida antes que nada: así ni la consulta ni la auditoría reciben
        // un valor que no haya pasado sus reglas.
        var user = User.Create(new UserCreationArgs(Guid.NewGuid(), command.Username, command.Email, actorId), clock.GetUtcNow());

        // Prefijo propio: «user:» identifica a un usuario existente por su id, y un nombre
        // con forma de GUID no debe poder pasar por uno.
        var candidate = $"username:{user.Username}";

        if (await userRepository.ExistsWithUsernameAsync(user.Username, cancellationToken)) {
            AuditLog.Rejected(logger, actorId, Action, candidate, "username already exists");
            return Result<CreateUserResult>.Failure(AlreadyExists);
        }

        if (await userRepository.ExistsWithEmailAsync(user.Email.Value, cancellationToken)) {
            AuditLog.Rejected(logger, actorId, Action, candidate, "email already exists");
            return Result<CreateUserResult>.Failure(AlreadyExists);
        }

        userRepository.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        AuditLog.Succeeded(logger, actorId, Action, $"user:{user.Id}");

        return Result<CreateUserResult>.Success(new CreateUserResult(user.Id, user.Username, user.Email.Value));

    }

}