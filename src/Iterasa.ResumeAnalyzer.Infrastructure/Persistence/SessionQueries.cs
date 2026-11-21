using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence;

/// <summary>
/// Lado de lectura de la sesión. Las membresías de un usuario cruzan empresas, así que se leen
/// sin el filtro de empresa y con el del usuario.
/// </summary>
internal sealed class SessionQueries(AppDbContext dbContext) : ISessionQueries {

    /// <inheritdoc />
    public Task<SessionUserRow?> FindUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && user.IsEnabled)
            .Select(user => new SessionUserRow(user.Id, user.Name, user.Email.Value, user.JobTitle, user.EmailVerifiedAtUtc != null))
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<MembershipRow>> ListMembershipsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await (
            from membership in dbContext.Memberships.IgnoreQueryFilters().AsNoTracking()
            join tenant in dbContext.Tenants.AsNoTracking() on membership.TenantId equals tenant.Id
            where membership.UserId == userId && membership.IsEnabled && tenant.IsEnabled
            orderby tenant.Name
            select new MembershipRow(tenant.Id, tenant.Name, membership.Role, membership.AnonymousReview, membership.CreatedAtUtc))
            .ToListAsync(cancellationToken);

}