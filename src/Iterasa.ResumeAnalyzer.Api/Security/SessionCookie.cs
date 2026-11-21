using Iterasa.ResumeAnalyzer.Application.Features.Auth;

namespace Iterasa.ResumeAnalyzer.Api.Security;

/// <summary>
/// La cookie de sesión: <c>HttpOnly</c> (el script de la web nunca la lee), <c>SameSite=Lax</c>
/// y, fuera de desarrollo, <c>Secure</c> con prefijo <c>__Host-</c> (<see cref="BrowserCookies" />).
/// </summary>
/// <remarks>
/// Sin «Recordarme» es de sesión del navegador; con él dura hasta el vencimiento absoluto. En
/// ambos casos la vigencia real la decide el servidor.
/// </remarks>
internal static class SessionCookie {

    public static string Name(HttpContext context) => Cookies(context).Session;

    public static void Append(HttpContext context, IssuedSession session) =>
        context.Response.Cookies.Append(Name(context), session.Token, Options(context, session.Persistent ? session.ExpiresAtUtc : null));

    public static void Delete(HttpContext context) => context.Response.Cookies.Delete(Name(context), Options(context, expires: null));

    private static CookieOptions Options(HttpContext context, DateTimeOffset? expires) => new() {
        HttpOnly = true,
        Secure = Cookies(context).IsSecure(context.Request),
        SameSite = SameSiteMode.Lax,
        Path = "/",
        IsEssential = true,
        Expires = expires,
    };

    private static BrowserCookies Cookies(HttpContext context) => context.RequestServices.GetRequiredService<BrowserCookies>();

}