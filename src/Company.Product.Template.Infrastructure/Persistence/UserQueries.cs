using Company.Product.Template.Application.Abstractions;
using Company.Product.Template.Application.Features.Users.Queries.GetUserById;
using Company.Product.Template.Application.Features.Users.Queries.GetUsers;
using Company.Product.Template.Shared;
using Microsoft.EntityFrameworkCore;

namespace Company.Product.Template.Infrastructure.Persistence;

/// <summary>
/// Lado de lectura de usuarios: proyecciones para los endpoints.
/// </summary>
/// <remarks>
/// Las consultas seleccionan columna a columna: el agregado no se materializa para leer.
/// </remarks>
internal sealed class UserQueries(AppDbContext dbContext) : IUserQueries {

    /// <inheritdoc />
    public async Task<GetUserByIdResult?> FindDetailByIdAsync(Guid id, CancellationToken cancellationToken = default) {
        return await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new GetUserByIdResult(u.Id, u.Username, u.Email.Value, u.IsEnabled, u.CreatedAtUtc, u.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResult<UserSummary>> ListAsync(PageQuery page, CancellationToken cancellationToken = default) {

        var query = dbContext.Users.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);

        IReadOnlyList<UserSummary> items = await query
            .OrderBy(u => u.Username)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(u => new UserSummary(u.Id, u.Username, u.Email.Value, u.IsEnabled, u.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserSummary>(items, page.Page, page.PageSize, totalCount);

    }

}