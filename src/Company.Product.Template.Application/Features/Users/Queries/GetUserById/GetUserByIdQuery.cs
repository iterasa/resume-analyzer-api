namespace Company.Product.Template.Application.Features.Users.Queries.GetUserById;

/// <summary>Consulta de usuario por identificador.</summary>
/// <param name="UserId">Identificador del usuario.</param>
public sealed record GetUserByIdQuery(Guid UserId);