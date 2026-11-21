using Microsoft.AspNetCore.WebUtilities;

namespace Iterasa.ResumeAnalyzer.Api.Problems;

/// <summary>
/// Identificadores de tipo de problema (RFC 9457) que publica la API.
/// </summary>
/// <remarks>
/// El miembro <c>type</c> es el identificador estable que un cliente usa para decidir;
/// <c>title</c> es un resumen legible por tipo y <c>detail</c> la explicación de la
/// ocurrencia. Toda respuesta de error de esta API, la construya un controller, el
/// manejador de excepciones o el propio framework, sale con un <c>type</c>
/// de este catálogo: es una única forma de error para el llamante.
/// El prefijo URN evita depender de un dominio que la plantilla no conoce; cada proyecto
/// puede sustituirlo por una URL que documente sus errores.
/// </remarks>
internal static class ProblemTypes {

    /// <summary>Prefijo de todo tipo de problema publicado por la API.</summary>
    public const string Prefix = "urn:problem-type:";

    /// <summary>Código de estado con el que se responde a una petición cancelada por el cliente.</summary>
    public const int ClientClosedRequestStatusCode = 499;

    /// <summary>
    /// Tipo de problema de un código de error de aplicación.
    /// </summary>
    /// <param name="code">Código estable del error (<c>not_found</c>, <c>invalid_credentials</c>…).</param>
    /// <returns>Identificador del tipo de problema.</returns>
    public static string For(string code) => Prefix + code;

    /// <summary>
    /// Tipo de problema por defecto de un código de estado HTTP.
    /// </summary>
    /// <param name="statusCode">Código de estado.</param>
    /// <returns>Identificador del tipo de problema.</returns>
    public static string ForStatus(int statusCode) => For(statusCode switch {
        StatusCodes.Status400BadRequest => Shared.Error.ValidationCode,
        StatusCodes.Status401Unauthorized => "unauthorized",
        StatusCodes.Status403Forbidden => "forbidden",
        StatusCodes.Status404NotFound => "not_found",
        StatusCodes.Status405MethodNotAllowed => "method_not_allowed",
        StatusCodes.Status406NotAcceptable => "not_acceptable",
        StatusCodes.Status409Conflict => "conflict",
        StatusCodes.Status413PayloadTooLarge => "payload_too_large",
        StatusCodes.Status415UnsupportedMediaType => "unsupported_media_type",
        StatusCodes.Status422UnprocessableEntity => "unprocessable_entity",
        StatusCodes.Status429TooManyRequests => "too_many_requests",
        ClientClosedRequestStatusCode => "request_canceled",
        >= StatusCodes.Status500InternalServerError => "unexpected_error",
        _ => $"http_{statusCode}"
    });

    /// <summary>
    /// Título legible de un código de estado. Estable por tipo, no por ocurrencia.
    /// </summary>
    /// <param name="statusCode">Código de estado.</param>
    /// <returns>Resumen legible.</returns>
    public static string TitleForStatus(int statusCode) => statusCode switch {
        StatusCodes.Status400BadRequest => Shared.Error.ValidationSummary,
        ClientClosedRequestStatusCode => "Client Closed Request",
        _ => ReasonPhrases.GetReasonPhrase(statusCode) is { Length: > 0 } phrase ? phrase : "Error"
    };

    /// <summary>
    /// Indica si el tipo pertenece a este catálogo. Los que no, los ha puesto el framework
    /// y se sustituyen por el que corresponda al código de estado.
    /// </summary>
    /// <param name="type">Tipo de problema a comprobar.</param>
    /// <returns><see langword="true" /> si lo publicó esta API.</returns>
    public static bool IsPublished(string? type) => type?.StartsWith(Prefix, StringComparison.Ordinal) == true;

}