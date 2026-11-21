using Iterasa.ResumeAnalyzer.Api.Logging;
using Iterasa.ResumeAnalyzer.Shared.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Iterasa.ResumeAnalyzer.Api.Security;

/// <summary>Exige un permiso del catálogo en la empresa activa.</summary>
/// <param name="Permission">Permiso.</param>
internal sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>
/// Resuelve <see cref="PermissionRequirement" /> con el rol de la sesión, que la autenticación
/// acaba de leer de la base: un cambio de rol vale desde la petición siguiente.
/// </summary>
internal sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement> {

    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement) {

        if (Enum.TryParse<MemberRole>(context.User.FindFirst(SessionClaims.Role)?.Value, out var role)
            && RolePermissions.Grants(role, requirement.Permission)) {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;

    }

}

/// <summary>
/// Deja rastro de cada autorización denegada antes de responder 403: qué permiso exigía el
/// endpoint y quién lo pedía. La respuesta la produce el manejador por defecto.
/// </summary>
internal sealed class AuditingAuthorizationResultHandler(ILogger<AuditingAuthorizationResultHandler> logger) : IAuthorizationMiddlewareResultHandler {

    private readonly AuthorizationMiddlewareResultHandler defaultHandler = new();

    /// <inheritdoc />
    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult) {

        if (authorizeResult.Forbidden) {
            logger.LogWarning(
                "Audit {AuditAction} on {AuditTarget} by {ActorId}: {AuditOutcome} (requires {RequiredPermissions}; tenant {TenantId}; correlation {CorrelationId})",
                "authorization.check",
                $"{context.Request.Method} {context.Request.Path}",
                context.User.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous",
                "denied",
                policy.Requirements.OfType<PermissionRequirement>().Select(requirement => requirement.Permission).ToArray(),
                context.User.FindFirst(SessionClaims.TenantId)?.Value,
                context.GetCorrelationId());
        }

        return defaultHandler.HandleAsync(next, context, policy, authorizeResult);

    }

}