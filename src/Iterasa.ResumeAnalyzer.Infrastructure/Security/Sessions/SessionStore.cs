using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Application.Features.Auth;
using Iterasa.ResumeAnalyzer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.Sessions;

/// <summary>
/// Sesiones opacas guardadas en PostgreSQL (ADR 0017): abrir, validar, renovar y revocar.
/// </summary>
internal sealed class SessionStore(AppDbContext dbContext, IOptions<SessionOptions> options, TimeProvider clock) : ISessionStore, ISessionAuthenticator {

    /// <inheritdoc />
    public IssuedSession Open(Guid userId, Guid tenantId, bool persistent, DateTimeOffset now, DateTimeOffset? absoluteExpiresAtUtc = null) {

        var token = SessionTokens.New();
        var absolute = absoluteExpiresAtUtc ?? now.Add(options.Value.AbsoluteLifetime);

        var session = new UserSession {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            TokenHash = SessionTokens.Hash(token),
            Persistent = persistent,
            CreatedAtUtc = now,
            LastSeenAtUtc = now,
            IdleExpiresAtUtc = Min(now.Add(options.Value.IdleTimeout), absolute),
            AbsoluteExpiresAtUtc = absolute,
        };

        dbContext.Sessions.Add(session);

        return new IssuedSession(session.Id, token, absolute, persistent);

    }

    /// <inheritdoc />
    /// <remarks>
    /// Se escribe al instante, no con la unidad de trabajo, y con la condición en la propia
    /// sentencia: de dos peticiones simultáneas sobre la misma sesión solo una afecta a la fila.
    /// La otra recibe nulo y no abre una sesión nueva.
    /// </remarks>
    public async Task<RevokedSession?> RevokeAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken = default) {

        var session = await dbContext.Sessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new { s.Persistent, s.AbsoluteExpiresAtUtc })
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null) {
            return null;
        }

        var revoked = await dbContext.Sessions
            .Where(s => s.Id == sessionId && s.RevokedAtUtc == null && s.IdleExpiresAtUtc > now && s.AbsoluteExpiresAtUtc > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.RevokedAtUtc, now), cancellationToken);

        return revoked == 1 ? new RevokedSession(session.Persistent, session.AbsoluteExpiresAtUtc) : null;

    }

    /// <inheritdoc />
    /// <remarks>
    /// Una sola consulta: sesión vigente, usuario y empresa activos y membresía activa con su rol
    /// actual. Las membresías se leen sin el filtro de empresa porque aún no hay empresa abierta:
    /// la condición la pone la propia sesión.
    /// </remarks>
    public async Task<AuthenticatedSession?> AuthenticateAsync(string token, CancellationToken cancellationToken = default) {

        if (!SessionTokens.IsWellFormed(token)) {
            return null;
        }

        var hash = SessionTokens.Hash(token);
        var now = clock.GetUtcNow();

        var found = await (
            from session in dbContext.Sessions.AsNoTracking()
            where session.TokenHash == hash && session.RevokedAtUtc == null && session.IdleExpiresAtUtc > now && session.AbsoluteExpiresAtUtc > now
            join membership in dbContext.Memberships.IgnoreQueryFilters().AsNoTracking()
                on new { session.TenantId, session.UserId } equals new { membership.TenantId, membership.UserId }
            join user in dbContext.Users.AsNoTracking() on session.UserId equals user.Id
            join tenant in dbContext.Tenants.AsNoTracking() on session.TenantId equals tenant.Id
            where membership.IsEnabled && user.IsEnabled && tenant.IsEnabled
            select new { session.Id, session.UserId, session.TenantId, membership.Role, session.LastSeenAtUtc, session.AbsoluteExpiresAtUtc })
            .FirstOrDefaultAsync(cancellationToken);

        if (found is null) {
            return null;
        }

        if (now - found.LastSeenAtUtc >= options.Value.RenewalInterval) {
            var idle = Min(now.Add(options.Value.IdleTimeout), found.AbsoluteExpiresAtUtc);
            await dbContext.Sessions
                .Where(session => session.Id == found.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(session => session.LastSeenAtUtc, now)
                    .SetProperty(session => session.IdleExpiresAtUtc, idle), cancellationToken);
        }

        return new AuthenticatedSession(found.Id, found.UserId, found.TenantId, found.Role);

    }

    /// <summary>Borra las sesiones vencidas o revocadas hace más de un día.</summary>
    public Task<int> PurgeAsync(DateTimeOffset now, CancellationToken cancellationToken) {
        var threshold = now.AddDays(-1);
        return dbContext.Sessions
            .Where(session => session.AbsoluteExpiresAtUtc < threshold || session.IdleExpiresAtUtc < threshold || session.RevokedAtUtc < threshold)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left < right ? left : right;

}