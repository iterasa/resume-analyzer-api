using Asp.Versioning;
using Iterasa.ResumeAnalyzer.Api.RateLimiting;
using Iterasa.ResumeAnalyzer.Api.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Iterasa.ResumeAnalyzer.Api.Controllers;

/// <summary>
/// Token antiforgery (CSRF). La web lo pide antes de su primera mutación y lo reenvía en
/// <c>X-XSRF-TOKEN</c>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/antiforgery")]
[EnableRateLimiting(RateLimitPolicies.PerClient)]
public sealed class AntiforgeryController(IAntiforgery antiforgery) : ControllerBase {

    /// <summary>Emite el token: deja la cookie <c>XSRF-TOKEN</c>, legible por la web.</summary>
    /// <remarks>Anónimo: también lo necesitan el inicio de sesión y el registro.</remarks>
    /// <response code="204">Token emitido en la cookie.</response>
    [HttpGet("token")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult GetToken() {
        antiforgery.IssueTokens(HttpContext, HttpContext.User);
        return NoContent();
    }

}