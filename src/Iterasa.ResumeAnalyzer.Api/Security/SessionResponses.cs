using Iterasa.ResumeAnalyzer.Application.Features.Auth;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Iterasa.ResumeAnalyzer.Api.Security;

/// <summary>
/// Respuestas que abren o cierran una sesión: la cookie de sesión y un token antiforgery nuevo
/// para la identidad que tendrán las peticiones siguientes. El token de sesión nunca va en el cuerpo.
/// </summary>
internal static class SessionResponses {

    public static IActionResult SignedIn(this ControllerBase controller, SignedIn signedIn, IAntiforgery antiforgery, int statusCode = StatusCodes.Status200OK) {

        SessionCookie.Append(controller.HttpContext, signedIn.Credential);

        antiforgery.IssueTokens(
            controller.HttpContext,
            SessionPrincipal.Create(signedIn.Session.User.Id, signedIn.Credential.SessionId, signedIn.Session.Tenant.Id, signedIn.Session.Role));

        return controller.StatusCode(statusCode, signedIn.Session);

    }

    public static IActionResult SignedOut(this ControllerBase controller, IAntiforgery antiforgery) {
        SessionCookie.Delete(controller.HttpContext);
        antiforgery.IssueTokens(controller.HttpContext, new ClaimsPrincipal(new ClaimsIdentity()));
        return controller.NoContent();
    }

}