using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Iterasa.ResumeAnalyzer.IntegrationTests;

/// <summary>
/// Sesión de prueba: autentica la petición que trae <see cref="Header" /> con el id del
/// usuario. Sin la cabecera la petición queda anónima y la API responde como en producción.
/// </summary>
internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder) {

    public const string SchemeName = "Test";
    public const string Header = "X-Test-User";

    /// <summary>Usuario con el que actúan por defecto los clientes de la factory.</summary>
    public static readonly Guid DefaultUserId = Guid.Parse("7e57a11c-0000-4000-8000-000000000001");

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
        if (!Request.Headers.TryGetValue(Header, out var value) || !Guid.TryParse(value, out var userId)) {
            return Task.FromResult(AuthenticateResult.NoResult());
        }
        var identity = new ClaimsIdentity([new Claim("sub", userId.ToString())], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }

}