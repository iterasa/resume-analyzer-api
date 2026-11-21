using Company.Product.Template.Shared;

namespace Company.Product.Template.Application.Features.Users.Queries.GetUserById;

/// <summary>Consulta de usuario por identificador.</summary>
public interface IGetUserByIdHandler {
    /// <summary>Ejecuta el caso de uso.</summary>
    Task<Result<GetUserByIdResult>> Handle(GetUserByIdQuery command, CancellationToken cancellationToken = default);
}