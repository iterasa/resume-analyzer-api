using Iterasa.ResumeAnalyzer.Api.Logging;
using Microsoft.Net.Http.Headers;

namespace Iterasa.ResumeAnalyzer.Api.Http;

/// <summary>
/// Política CORS de la API, alimentada desde variables de entorno.
/// </summary>
/// <remarks>
/// Una única política, por lista de orígenes explícita. Métodos y cabeceras no se
/// configuran: son los que la API usa, y abrirlos a cualquiera no da nada al cliente
/// legítimo y sí a un tercero. Si un proyecto añade <c>PATCH</c> o una cabecera propia,
/// la añade aquí, en un sitio.
/// </remarks>
internal sealed class CorsPolicyOptions {

    /// <summary>Segundos por defecto que el navegador cachea un preflight.</summary>
    public const int DefaultPreflightMaxAgeSeconds = 600;

    /// <summary>Métodos que un navegador puede invocar desde otro origen.</summary>
    public static readonly string[] AllowedMethods = ["GET", "POST", "PUT", "DELETE"];

    /// <summary>Cabeceras que un navegador puede enviar desde otro origen.</summary>
    public static readonly string[] AllowedHeaders = [HeaderNames.Authorization, HeaderNames.ContentType, CorrelationIdExtensions.HeaderName];

    /// <summary>
    /// Cabeceras de respuesta que el script del navegador puede leer. Sin esta lista solo
    /// ve las «seguras» (<c>Content-Type</c> y poco más): ni la correlación para reportar
    /// un error, ni <c>Retry-After</c> tras un 429, ni <c>Location</c> tras un 201.
    /// </summary>
    public static readonly string[] ExposedHeaders = [CorrelationIdExtensions.HeaderName, HeaderNames.RetryAfter, HeaderNames.Location];

    /// <summary>Indica si la política está activa. Desactivada, la API no emite cabecera CORS alguna.</summary>
    public bool Enabled { get; set; }

    /// <summary>Orígenes admitidos, tal y como llegaron de la configuración. Se validan al arrancar.</summary>
    public IReadOnlyList<string> AllowedOrigins { get; set; } = [];

    /// <summary>Permite peticiones con credenciales de navegador (cookies, autenticación HTTP).</summary>
    public bool AllowCredentials { get; set; }

    /// <summary>Segundos que el navegador puede cachear la respuesta a un preflight.</summary>
    public int PreflightMaxAgeSeconds { get; set; } = DefaultPreflightMaxAgeSeconds;

}