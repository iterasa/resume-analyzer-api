using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Domain.Tenants;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using Microsoft.EntityFrameworkCore;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence;

/// <summary>Lado de escritura de empresas y membresías.</summary>
/// <remarks>
/// Las membresías de un usuario cruzan empresas: estas lecturas ignoran el filtro de empresa
/// y llevan el suyo explícito (la empresa y el usuario que se consultan).
/// </remarks>
internal sealed class TenantRepository(AppDbContext dbContext, EncryptionKeyProvisioner keys) : ITenantRepository {

    /// <inheritdoc />
    public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default) {
        dbContext.Tenants.Add(tenant);
        await keys.ProvisionScopeAsync(dbContext, tenant.Id, cancellationToken);
    }

    /// <inheritdoc />
    public void AddMembership(Membership membership) => dbContext.Memberships.Add(membership);

    /// <inheritdoc />
    public Task<Guid?> FindDefaultTenantAsync(Guid userId, CancellationToken cancellationToken = default) =>
        ActiveMemberships()
            .Where(membership => membership.UserId == userId)
            .OrderBy(membership => membership.CreatedAtUtc)
            .Select(membership => (Guid?)membership.TenantId)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> IsActiveMemberAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
        ActiveMemberships().AnyAsync(membership => membership.TenantId == tenantId && membership.UserId == userId, cancellationToken);

    private IQueryable<Membership> ActiveMemberships() =>
        from membership in dbContext.Memberships.IgnoreQueryFilters().AsNoTracking()
        join tenant in dbContext.Tenants.AsNoTracking() on membership.TenantId equals tenant.Id
        join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
        where membership.IsEnabled && tenant.IsEnabled && user.IsEnabled
        select membership;

}