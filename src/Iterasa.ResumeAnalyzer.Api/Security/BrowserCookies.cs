namespace Iterasa.ResumeAnalyzer.Api.Security;

/// <summary>
/// Nombres y atributos de las cookies que la API deja en el navegador (ADR 0017).
/// </summary>
/// <remarks>
/// <para>Fuera de <c>Development</c> todas son <c>Secure</c> y las que no lee la web llevan el
/// prefijo <c>__Host-</c>: el navegador solo las acepta por HTTPS, sin <c>Domain</c> y con
/// <c>Path=/</c>, así que ningún subdominio puede fijarlas ni pisarlas.</para>
/// <para>En <c>Development</c> la web llega por <c>http://localhost</c> (el proxy de Vite): ahí
/// van sin prefijo y <c>Secure</c> solo si la petición es HTTPS. El antiforgery de ASP.NET se
/// niega a emitir una cookie <c>Secure</c> en una petición HTTP.</para>
/// <para>Tras un proxy que termina TLS, la petición solo es HTTPS para la API si
/// <c>FORWARDED_HEADERS_ENABLED</c> está activo.</para>
/// </remarks>
internal sealed class BrowserCookies {

    /// <summary>Cookie de sesión (<c>HttpOnly</c>).</summary>
    public required string Session { get; init; }

    /// <summary>Cookie del antiforgery (<c>HttpOnly</c>), pareja del token que lee la web.</summary>
    public required string Antiforgery { get; init; }

    /// <summary>Cookie legible por la web con el token antiforgery que debe reenviar.</summary>
    public string RequestToken => AntiforgeryExtensions.RequestTokenCookieName;

    /// <summary>Si todas son <c>Secure</c> siempre; si no, solo en peticiones HTTPS.</summary>
    public required bool AlwaysSecure { get; init; }

    /// <summary>Política del entorno.</summary>
    public static BrowserCookies For(IHostEnvironment environment) => environment.IsDevelopment()
        ? new() { Session = "criba_session", Antiforgery = "criba_af", AlwaysSecure = false }
        : new() { Session = "__Host-criba_session", Antiforgery = "__Host-criba_af", AlwaysSecure = true };

    /// <summary>Atributo <c>Secure</c> para una respuesta concreta.</summary>
    public bool IsSecure(HttpRequest request) => AlwaysSecure || request.IsHttps;

}