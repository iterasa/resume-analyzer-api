using Iterasa.ResumeAnalyzer.Api.Problems;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Security.Claims;

namespace Iterasa.ResumeAnalyzer.Api.Security;

/// <summary>
/// Protección CSRF con el antiforgery de ASP.NET Core (ADR 0017): la web lee el token de la
/// cookie <c>XSRF-TOKEN</c> y lo reenvía en <c>X-XSRF-TOKEN</c> en cada POST, PUT, PATCH y DELETE.
/// </summary>
/// <remarks>
/// El token va ligado a la identidad: tras iniciar o cerrar sesión se emite otro. Fuera de
/// desarrollo, la cookie <c>HttpOnly</c> lleva el prefijo <c>__Host-</c>, así que un subdominio no
/// puede plantar un par válido (<see cref="BrowserCookies" />). Las llaves con las que se firman
/// viven cifradas en la base (Data Protection).
/// </remarks>
internal static class AntiforgeryExtensions {

    /// <summary>Cookie legible por la web con el token que debe reenviar.</summary>
    public const string RequestTokenCookieName = "XSRF-TOKEN";

    /// <summary>Cabecera en la que la web reenvía el token.</summary>
    public const string HeaderName = "X-XSRF-TOKEN";

    /// <summary>Código de error que la web reconoce para pedir otro token y reintentar una vez.</summary>
    public const string InvalidCode = "antiforgery_invalid";

    public static IServiceCollection AddConfiguredAntiforgery(this IServiceCollection services) {

        services.AddSingleton(provider => BrowserCookies.For(provider.GetRequiredService<IHostEnvironment>()));

        services.AddAntiforgery();
        services
            .AddOptions<AntiforgeryOptions>()
            .Configure<BrowserCookies>((options, cookies) => {
                options.HeaderName = HeaderName;
                options.Cookie.Name = cookies.Antiforgery;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = cookies.AlwaysSecure ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.Path = "/";
                // La cabecera de marcos la pone SecurityHeadersExtensions (DENY) en toda respuesta.
                options.SuppressXFrameOptionsHeader = true;
            });

        services.AddScoped<AntiforgeryValidationFilter>();

        return services;

    }

    /// <summary>
    /// Emite el par de tokens para <paramref name="user" /> (la identidad que tendrán las
    /// peticiones siguientes) y deja el de petición en la cookie legible.
    /// </summary>
    public static void IssueTokens(this IAntiforgery antiforgery, HttpContext context, ClaimsPrincipal user) {

        var previous = context.User;
        context.User = user;

        try {
            var tokens = antiforgery.GetAndStoreTokens(context);
            context.Response.Cookies.Append(RequestTokenCookieName, tokens.RequestToken!, new CookieOptions {
                HttpOnly = false,
                Secure = context.RequestServices.GetRequiredService<BrowserCookies>().IsSecure(context.Request),
                SameSite = SameSiteMode.Strict,
                Path = "/",
                IsEssential = true,
            });
        } finally {
            context.User = previous;
        }

    }

}

/// <summary>
/// Valida el token antiforgery en todo método que cambia estado, en todos los controllers:
/// cerrado por defecto, como la autenticación.
/// </summary>
internal sealed class AntiforgeryValidationFilter(
    IAntiforgery antiforgery,
    ProblemDetailsFactory problemDetailsFactory,
    ILogger<AntiforgeryValidationFilter> logger) : IAsyncAuthorizationFilter {

    /// <inheritdoc />
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context) {

        var method = context.HttpContext.Request.Method;

        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method)) {
            return;
        }

        try {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        } catch (AntiforgeryValidationException exception) {
            logger.LogWarning("Antiforgery validation failed for {RequestMethod} {RequestPath}: {Reason}", method, context.HttpContext.Request.Path, exception.Message);
            var problem = problemDetailsFactory.CreateProblemDetails(
                context.HttpContext,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                type: ProblemTypes.For(AntiforgeryExtensions.InvalidCode),
                detail: "The antiforgery token is missing or invalid. Request a new one and retry.");
            context.Result = new ObjectResult(problem) {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = { ProblemResponseContentTypeConvention.ContentType },
            };
        }

    }

}