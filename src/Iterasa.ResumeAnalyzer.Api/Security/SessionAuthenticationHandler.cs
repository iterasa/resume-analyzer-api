using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Shared.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Iterasa.ResumeAnalyzer.Api.Security;

/// <summary>
/// Autentica la petición con la cookie de sesión: un token opaco que se valida contra la base
/// en cada petición (ADR 0017). Sin cookie, la petición es anónima; con una que no vale, falla
/// y la ruta protegida responde 401.
/// </summary>
internal sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISessionAuthenticator sessions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder) {

    /// <summary>Nombre del esquema.</summary>
    public const string SchemeName = "Session";

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync() {

        if (!Request.Cookies.TryGetValue(SessionCookie.Name(Context), out var token) || string.IsNullOrEmpty(token)) {
            return AuthenticateResult.NoResult();
        }

        var session = await sessions.AuthenticateAsync(token, Context.RequestAborted);

        if (session is null) {
            // Vencida o revocada: la cookie ya no sirve y se retira.
            SessionCookie.Delete(Context);
            return AuthenticateResult.Fail("The session is not valid.");
        }

        var principal = SessionPrincipal.Create(session.UserId, session.SessionId, session.TenantId, session.Role);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));

    }

}

/// <summary>El principal de una sesión: siempre los mismos claims, venga de la cookie o de un inicio de sesión.</summary>
internal static class SessionPrincipal {

    public static ClaimsPrincipal Create(Guid userId, Guid sessionId, Guid tenantId, MemberRole role) =>
        new(new ClaimsIdentity(
            [
                new Claim(SessionClaims.UserId, userId.ToString("D", CultureInfo.InvariantCulture)),
                new Claim(SessionClaims.SessionId, sessionId.ToString("D", CultureInfo.InvariantCulture)),
                new Claim(SessionClaims.TenantId, tenantId.ToString("D", CultureInfo.InvariantCulture)),
                new Claim(SessionClaims.Role, role.ToString()),
            ],
            SessionAuthenticationHandler.SchemeName,
            SessionClaims.UserId,
            SessionClaims.Role));

}