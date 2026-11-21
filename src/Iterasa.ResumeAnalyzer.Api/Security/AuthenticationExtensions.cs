using Iterasa.ResumeAnalyzer.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Iterasa.ResumeAnalyzer.Api.Security;

/// <summary>
/// Autenticación y autorización: cerrada por defecto (ADR 0016), con sesión del lado del
/// servidor (ADR 0017) y permisos por rol en la empresa activa (ADR 0018).
/// </summary>
/// <remarks>
/// Toda ruta exige una sesión salvo que se declare anónima de forma explícita. Además, cada
/// permiso del catálogo es una política con su mismo nombre: un endpoint declara lo que
/// necesita (<c>[Authorize(Policy = Permissions.VacanciesManage)]</c>), no «ser administrador».
/// </remarks>
internal static class AuthenticationExtensions {

    /// <summary>Registra el esquema de sesión, la política por defecto y una política por permiso.</summary>
    public static IServiceCollection AddConfiguredAuthentication(this IServiceCollection services) {

        services
            .AddAuthentication(SessionAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, configureOptions: null);

        var authorization = services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        foreach (var permission in Permissions.All) {
            authorization.AddPolicy(permission, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission)));
        }

        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuditingAuthorizationResultHandler>();

        return services;

    }

}