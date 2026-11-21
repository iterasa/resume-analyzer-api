using Iterasa.ResumeAnalyzer.Api.Problems;
using Iterasa.ResumeAnalyzer.Shared;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Iterasa.ResumeAnalyzer.Api;

/// <summary>
/// Extensiones de la pipeline de middlewares relacionadas con el manejo de excepciones.
/// </summary>
public static class ExceptionHandlerExtensions {

    /// <summary>
    /// Registra el middleware de manejo global de excepciones no controladas.
    /// Registra la traza estructurada y retorna una respuesta Problem Details (RFC 9457).
    /// </summary>
    /// <remarks>
    /// Es también el único punto donde una invariante de dominio incumplida
    /// (<see cref="DomainException" />) se traduce a respuesta. Concentrarla aquí evita
    /// que cada handler tenga que capturarla y, sobre todo, que un error de programación
    /// se confunda con un error de validación del llamante.
    /// </remarks>
    /// <param name="app">Pipeline de la aplicación.</param>
    /// <returns>El mismo pipeline para encadenamiento.</returns>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app) {

        app.UseExceptionHandler(errorApp => {
            errorApp.Run(async context => {

                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

                var logger = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(ExceptionHandlerExtensions).FullName!);

                var (statusCode, detail) = Describe(exception);

                if (exception is OperationCanceledException) {
                    logger.LogWarning(
                        "Request {RequestMethod} {RequestPath} was canceled before completing",
                        context.Request.Method,
                        context.Request.Path);
                } else if (exception is DomainException or ConcurrencyException or BadHttpRequestException) {
                    logger.LogWarning(
                        "Request {RequestMethod} {RequestPath} rejected with {StatusCode}: {RejectionReason}",
                        context.Request.Method,
                        context.Request.Path,
                        statusCode,
                        exception.Message);
                } else {
                    logger.LogError(
                        exception,
                        "Unhandled exception while processing {RequestMethod} {RequestPath}",
                        context.Request.Method,
                        context.Request.Path);
                }

                if (context.Response.HasStarted || context.RequestAborted.IsCancellationRequested) {
                    return;
                }

                context.Response.StatusCode = statusCode;

                await context.RequestServices
                    .GetRequiredService<IProblemDetailsService>()
                    .WriteAsync(new ProblemDetailsContext {
                        HttpContext = context,
                        ProblemDetails = BuildProblem(exception, statusCode, detail)
                    });

            });
        });

        return app;

    }

    /// <remarks>
    /// El mensaje de <see cref="DomainException" /> sí viaja al llamante: describe la
    /// regla que ha incumplido y es información que necesita para corregir la petición.
    /// El de cualquier otra excepción no, porque puede contener estado interno.
    /// </remarks>
    private static (int StatusCode, string Detail) Describe(Exception? exception) => exception switch {

        OperationCanceledException => (
            ProblemTypes.ClientClosedRequestStatusCode,
            "The request was canceled before it could be completed."),

        DomainException domainException => (
            StatusCodes.Status400BadRequest,
            domainException.Message),

        ConcurrencyException concurrencyException => (
            StatusCodes.Status409Conflict,
            concurrencyException.Message),

        // Kestrel la lanza al leer un cuerpo malformado o mayor que el límite: es un
        // error del llamante, con el código que el propio servidor ya ha decidido.
        BadHttpRequestException badRequest => (
            badRequest.StatusCode,
            "The request could not be read."),

        _ => (
            StatusCodes.Status500InternalServerError,
            "An unexpected error occurred. Please try again later.")

    };

    /// <remarks>
    /// Una invariante incumplida es, para el llamante, un error de validación: sale con
    /// la misma forma que los demás (<c>errors</c> por campo, aquí sin campo señalado).
    /// </remarks>
    private static ProblemDetails BuildProblem(Exception? exception, int statusCode, string detail) {

        var type = ProblemTypes.ForStatus(statusCode);
        var title = ProblemTypes.TitleForStatus(statusCode);

        if (exception is DomainException) {
            return new HttpValidationProblemDetails(new Dictionary<string, string[]>(StringComparer.Ordinal) { [string.Empty] = [detail] }) {
                Type = type,
                Title = title,
                Detail = detail,
                Status = statusCode
            };
        }

        return new ProblemDetails {
            Type = type,
            Title = title,
            Detail = detail,
            Status = statusCode
        };

    }

}