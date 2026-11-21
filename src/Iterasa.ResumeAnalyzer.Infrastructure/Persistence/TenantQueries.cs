using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence;

/// <summary>Lado de lectura de empresas.</summary>
internal sealed class TenantQueries(AppDbContext dbContext) : ITenantQueries {

    /// <inheritdoc />
    public Task<TenantSummaryRow?> FindSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        dbContext.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == tenantId && tenant.IsEnabled)
            .Select(tenant => new TenantSummaryRow(tenant.Id, tenant.Name, tenant.Country, tenant.PlanCode, tenant.PlanStatus, tenant.TrialEndsAtUtc, tenant.RetentionDays))
            .FirstOrDefaultAsync(cancellationToken);

}