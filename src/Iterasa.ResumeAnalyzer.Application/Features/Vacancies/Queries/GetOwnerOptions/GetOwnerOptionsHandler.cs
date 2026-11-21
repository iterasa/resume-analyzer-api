using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Queries.GetOwnerOptions;

/// <summary>
/// Caso de uso: miembros activos de la empresa que pueden ser responsables. Se cachea un minuto
/// por empresa, solo en memoria (lleva nombres).
/// </summary>
internal sealed class GetOwnerOptionsHandler(ICurrentPrincipal principal, ITeamQueries team, ICacheService cache) : IGetOwnerOptionsHandler {

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<OwnerOption>>> Handle(CancellationToken cancellationToken = default) {

        // La fábrica de la caché puede correr fuera del contexto de la petición: la empresa va explícita.
        var tenantId = principal.TenantId;

        return Result<IReadOnlyList<OwnerOption>>.Success(await cache.GetOrCreateAsync(
            CacheKeys.OwnerOptions(tenantId),
            token => new ValueTask<IReadOnlyList<OwnerOption>>(team.ListOwnerOptionsAsync(tenantId, token)),
            CacheKeys.OwnerOptionsPolicy,
            cancellationToken));

    }

}