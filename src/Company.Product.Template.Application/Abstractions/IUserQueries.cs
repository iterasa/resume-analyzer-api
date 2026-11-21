using Company.Product.Template.Application.Features.Users.Queries.GetUserById;
using Company.Product.Template.Application.Features.Users.Queries.GetUsers;
using Company.Product.Template.Shared;

namespace Company.Product.Template.Application.Abstractions;

/// <summary>
/// Puerto de lectura de usuarios.
/// </summary>
/// <remarks>
/// Los endpoints de consulta reciben proyecciones, nunca el agregado.
/// </remarks>
internal interface IUserQueries {

    Task<GetUserByIdResult?> FindDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<UserSummary>> ListAsync(PageQuery page, CancellationToken cancellationToken = default);

}