using Serilog.Context;

namespace Iterasa.ResumeAnalyzer.Api.Logging;

/// <summary>
/// Correlación de peticiones: propaga un identificador estable entre el cliente,
/// los logs y la respuesta HTTP.
/// </summary>
internal static class CorrelationIdExtensions {

    /// <summary>
    /// Nombre de la cabecera de correlación aceptada y devuelta.
    /// </summary>
    public const string HeaderName = "X-Correlation-Id";

    private const int MaxLength = 64;

    /// <summary>
    /// Registra el middleware de correlación. Debe ejecutarse antes del logging de peticiones.
    /// </summary>
    /// <param name="app">Pipeline de la aplicación.</param>
    /// <returns>El mismo pipeline para encadenamiento.</returns>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.Use(async (context, next) => {

            var correlationId = Sanitize(context.Request.Headers[HeaderName].FirstOrDefault())
                ?? context.TraceIdentifier;

            context.Items[HeaderName] = correlationId;
            context.Response.Headers[HeaderName] = correlationId;

            using (LogContext.PushProperty("CorrelationId", correlationId)) {
                await next(context);
            }

        });

    /// <summary>
    /// Obtiene el identificador de correlación de la petición en curso.
    /// </summary>
    /// <param name="context">Contexto HTTP.</param>
    /// <returns>Identificador de correlación.</returns>
    public static string GetCorrelationId(this HttpContext context) =>
        context.Items.TryGetValue(HeaderName, out var value) && value is string correlationId
            ? correlationId
            : context.TraceIdentifier;

    /// <remarks>
    /// El valor llega desde el exterior: se acota y se restringe el alfabeto para evitar
    /// inyección en cabeceras y en el log.
    /// </remarks>
    private static string? Sanitize(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= MaxLength
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
            ? value
            : null;

}