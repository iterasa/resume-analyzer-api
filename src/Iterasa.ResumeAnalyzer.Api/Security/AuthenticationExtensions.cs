using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

namespace Iterasa.ResumeAnalyzer.Api.Security;

/// <summary>
/// Autenticación y autorización de la API: cerrada por defecto (ADR 0016).
/// </summary>
/// <remarks>
/// Toda ruta exige un usuario autenticado salvo que se declare anónima de forma explícita
/// (las sondas de salud y el documento OpenAPI). Así, un endpoint nuevo nace protegido: olvidar
/// un atributo deja la ruta cerrada, no abierta.
/// </remarks>
internal static class AuthenticationExtensions {

    /// <summary>Esquema provisional mientras no exista el inicio de sesión.</summary>
    public const string PendingSessionScheme = "PendingSession";

    /// <summary>
    /// Registra el esquema y la política por defecto. Cuando llegue el inicio de sesión con
    /// cookie, sustituye a <see cref="PendingSessionScheme" />; la política no cambia.
    /// </summary>
    public static IServiceCollection AddConfiguredAuthentication(this IServiceCollection services) {

        services
            .AddAuthentication(PendingSessionScheme)
            .AddScheme<AuthenticationSchemeOptions, PendingSessionAuthenticationHandler>(PendingSessionScheme, configureOptions: null);

        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;

    }

}

/// <summary>
/// Todavía no hay inicio de sesión: ninguna petición queda autenticada, así que toda ruta
/// protegida responde 401 (Problem Details) en lugar de quedar abierta.
/// </summary>
internal sealed class PendingSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder) {

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());

}