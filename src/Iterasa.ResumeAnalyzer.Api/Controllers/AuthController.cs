using Asp.Versioning;
using Iterasa.ResumeAnalyzer.Api.Contracts;
using Iterasa.ResumeAnalyzer.Api.Extensions;
using Iterasa.ResumeAnalyzer.Api.RateLimiting;
using Iterasa.ResumeAnalyzer.Api.Security;
using Iterasa.ResumeAnalyzer.Application.Features.Auth.Commands.Register;
using Iterasa.ResumeAnalyzer.Application.Features.Auth.Commands.SignIn;
using Iterasa.ResumeAnalyzer.Application.Features.Auth.Commands.SignOut;
using Iterasa.ResumeAnalyzer.Application.Features.Auth.Commands.SwitchTenant;
using Iterasa.ResumeAnalyzer.Application.Features.Me;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Iterasa.ResumeAnalyzer.Api.Controllers;

/// <summary>
/// Sesión: registro, inicio y cierre, y cambio de empresa. La sesión es una cookie
/// <c>HttpOnly</c> con un token opaco; ninguna respuesta trae tokens.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[EnableRateLimiting(RateLimitPolicies.PerClient)]
public sealed class AuthController(
    IRegisterHandler registerHandler,
    ISignInHandler signInHandler,
    ISignOutHandler signOutHandler,
    ISwitchTenantHandler switchTenantHandler,
    IAntiforgery antiforgery) : ControllerBase {

    /// <summary>Registra una empresa con su primer administrador y abre su sesión.</summary>
    /// <remarks>El correo queda sin verificar. Limitado por IP.</remarks>
    /// <response code="201">Empresa registrada; sesión abierta.</response>
    /// <response code="400">Datos no válidos, términos sin aceptar o contraseña corta.</response>
    /// <response code="409">Ya existe una cuenta con ese correo.</response>
    /// <response code="429">Demasiados intentos desde esta IP.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    [ProducesResponseType(typeof(SessionView), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken) {
        var result = await registerHandler.Handle(
            new RegisterCommand(request.CompanyName, request.Country, request.Name, request.Email, request.Password, request.AcceptTerms),
            cancellationToken);
        return result.Match(signedIn => this.SignedIn(signedIn, antiforgery, StatusCodes.Status201Created), this.ToProblem);
    }

    /// <summary>Inicia sesión con correo y contraseña.</summary>
    /// <remarks>
    /// Tras cinco intentos fallidos la cuenta se bloquea quince minutos. La respuesta es la misma
    /// exista o no la cuenta. Limitado por IP.
    /// </remarks>
    /// <response code="200">Sesión abierta.</response>
    /// <response code="401">Correo o contraseña incorrectos (<c>invalid_credentials</c>).</response>
    /// <response code="429">Demasiados intentos desde esta IP.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    [ProducesResponseType(typeof(SessionView), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SignIn([FromBody] SignInRequest request, CancellationToken cancellationToken) {
        var result = await signInHandler.Handle(new SignInCommand(request.Email, request.Password, request.RememberMe), cancellationToken);
        return result.Match(signedIn => this.SignedIn(signedIn, antiforgery), this.ToProblem);
    }

    /// <summary>Cierra la sesión: la revoca en el servidor y retira la cookie.</summary>
    /// <response code="204">Sesión cerrada.</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SignOut(CancellationToken cancellationToken) {
        var result = await signOutHandler.Handle(cancellationToken);
        return result.Match(() => this.SignedOut(antiforgery), this.ToProblem);
    }

    /// <summary>Pasa a otra empresa del usuario. La sesión se sustituye por una nueva.</summary>
    /// <response code="200">Sesión en la otra empresa.</response>
    /// <response code="404">No es miembro activo de esa empresa.</response>
    [HttpPost("switch-tenant")]
    [ProducesResponseType(typeof(SessionView), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SwitchTenant([FromBody] SwitchTenantRequest request, CancellationToken cancellationToken) {
        var result = await switchTenantHandler.Handle(new SwitchTenantCommand(request.TenantId!.Value), cancellationToken);
        return result.Match(signedIn => this.SignedIn(signedIn, antiforgery), this.ToProblem);
    }

}