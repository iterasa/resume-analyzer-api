using Microsoft.Extensions.Primitives;

namespace Iterasa.ResumeAnalyzer.Api.Http;

/// <summary>
/// Cabeceras de respuesta que endurecen la API frente al navegador y a las caches intermedias.
/// </summary>
/// <remarks>
/// Son las que la guía de seguridad REST de OWASP pide a una API que solo sirve JSON:
/// que nadie reinterprete el contenido como HTML, que ninguna página la embeba en un
/// marco, que no se filtre la URL de origen y que ninguna cache conserve una respuesta
/// que casi siempre lleva datos de un usuario autenticado. HSTS no va aquí: la
/// plantilla asume que el TLS termina en el proxy inverso, y es él quien debe emitirla.
/// </remarks>
internal static class SecurityHeadersExtensions {

    /// <summary>
    /// Registra el middleware que añade las cabeceras de seguridad a toda respuesta.
    /// </summary>
    /// <remarks>
    /// Se aplican justo antes de enviar la respuesta, sea cual sea el camino que la
    /// produjo: un controller, el manejador de excepciones o el framework.
    /// <c>Cache-Control</c> solo se fija cuando nadie lo ha hecho, de modo que un
    /// endpoint puede declarar su propia política con <c>[ResponseCache]</c>.
    /// </remarks>
    /// <param name="app">Pipeline de la aplicación.</param>
    /// <returns>El mismo pipeline para encadenamiento.</returns>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) => {
            context.Response.OnStarting(static state => {

                var headers = ((HttpContext)state).Response.Headers;

                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers.ContentSecurityPolicy = "frame-ancestors 'none'";
                headers["Referrer-Policy"] = "no-referrer";

                if (StringValues.IsNullOrEmpty(headers.CacheControl)) {
                    headers.CacheControl = "no-store";
                }

                return Task.CompletedTask;

            }, context);

            return next(context);
        });

}