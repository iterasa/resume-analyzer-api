using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Application.Features.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence;

/// <summary>Lado de lectura del equipo de una empresa.</summary>
/// <remarks>
/// La empresa llega explícita (lo llama una fábrica de la caché, que puede correr fuera de la
/// petición), así que la consulta lleva su propio filtro en lugar del ambiental. Los nombres
/// están cifrados: se ordenan en memoria, ya descifrados (ADR 0015).
/// </remarks>
internal sealed class TeamQueries(AppDbContext dbContext) : ITeamQueries {

    /// <inheritdoc />
    public async Task<IReadOnlyList<OwnerOption>> ListOwnerOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default) {

        var userIds = await dbContext.Memberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(membership => membership.TenantId == tenantId && membership.IsEnabled)
            .Select(membership => membership.UserId)
            .ToListAsync(cancellationToken);

        var owners = await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id) && user.IsEnabled)
            .Select(user => new OwnerOption(user.Id, user.Name))
            .ToListAsync(cancellationToken);

        return [.. owners.OrderBy(owner => owner.Name, StringComparer.CurrentCultureIgnoreCase)];

    }

}