using Asp.Versioning;
using Iterasa.ResumeAnalyzer.Api.Extensions;
using Iterasa.ResumeAnalyzer.Api.RateLimiting;
using Iterasa.ResumeAnalyzer.Application.Features.Me;
using Iterasa.ResumeAnalyzer.Application.Features.Me.Queries.GetSession;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Iterasa.ResumeAnalyzer.Api.Controllers;

/// <summary>La sesión de quien llama.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/me")]
[EnableRateLimiting(RateLimitPolicies.PerClient)]
public sealed class MeController(IGetSessionHandler getSessionHandler) : ControllerBase {

    /// <summary>Usuario, empresa activa, membresías, rol, permisos y evaluación anónima efectiva.</summary>
    /// <response code="200">Sesión vigente.</response>
    /// <response code="401">Sin sesión, o vencida o revocada.</response>
    [HttpGet]
    [ProducesResponseType(typeof(SessionView), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) {
        var result = await getSessionHandler.Handle(cancellationToken);
        return result.Match(Ok, this.ToProblem);
    }

}