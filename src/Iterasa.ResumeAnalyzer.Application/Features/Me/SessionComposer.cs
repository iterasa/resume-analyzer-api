using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Domain.Tenants;
using Iterasa.ResumeAnalyzer.Shared.Tenancy;

namespace Iterasa.ResumeAnalyzer.Application.Features.Me;

/// <summary>
/// Arma la sesión que ve la web: la usan <c>GET /me</c> y todo lo que abre una sesión.
/// </summary>
internal sealed class SessionComposer(ISessionQueries sessions, ITenantQueries tenants, ICacheService cache) {

    /// <summary>Sesión del usuario en la empresa, o nula si ya no es miembro activo de ella.</summary>
    public async Task<SessionView?> ComposeAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken) {

        var user = await sessions.FindUserAsync(userId, cancellationToken);
        if (user is null) {
            return null;
        }

        var memberships = await sessions.ListMembershipsAsync(userId, cancellationToken);
        var current = memberships.SingleOrDefault(membership => membership.TenantId == tenantId);
        if (current is null) {
            return null;
        }

        var tenant = await cache.GetOrCreateAsync(
            CacheKeys.TenantSummary(tenantId),
            token => new ValueTask<TenantSummaryRow?>(tenants.FindSummaryAsync(tenantId, token)),
            CacheKeys.TenantSummaryPolicy,
            cancellationToken);

        if (tenant is null) {
            return null;
        }

        return new SessionView(
            new SessionUser(user.Id, user.Name, user.Email, user.JobTitle, user.EmailVerified),
            new SessionTenant(tenant.Id, tenant.Name, tenant.Country, new SessionPlan(tenant.PlanCode, PlanName(tenant.PlanCode), tenant.PlanStatus, tenant.TrialEndsAtUtc)),
            [.. memberships.Select(membership => new SessionMembership(membership.TenantId, membership.TenantName, membership.Role))],
            current.Role,
            RolePermissions.Of(current.Role),
            current.AnonymousReview || !Membership.CanReveal(current.Role));

    }

    /// <remarks>El catálogo de planes llega con Plan y pagos; mientras, el código basta.</remarks>
    private static string PlanName(string code) => code switch {
        Tenant.TrialPlanCode => "Prueba",
        _ => code,
    };

}