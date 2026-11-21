using Company.Product.Template.Api.Problems;
using Company.Product.Template.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Company.Product.Template.Api.Extensions;

/// <summary>
/// Traducción de errores de aplicación a respuestas HTTP.
/// </summary>
/// <remarks>
/// La correspondencia entre la clasificación semántica del error y el código de estado
/// vive aquí, en la capa que habla HTTP. El error que viaja desde los casos de uso no
/// conoce el protocolo, de modo que el mismo resultado sirve a un controller, a un job
/// o a un consumidor de mensajes.
/// </remarks>
internal static class ControllerBaseExtensions {

    /// <summary>
    /// Convierte un <see cref="Error" /> en una respuesta Problem Details (RFC 9457).
    /// </summary>
    /// <remarks>
    /// Un error de validación sale con la misma forma que la validación del contrato HTTP
    /// (<see cref="ValidationProblemDetails" />, con <c>errors</c> por campo), de modo que
    /// el llamante trata igual un campo obligatorio ausente y una regla de negocio incumplida.
    /// </remarks>
    /// <param name="controller">Controller que emite la respuesta.</param>
    /// <param name="error">Error devuelto por la capa de aplicación.</param>
    /// <returns>Respuesta HTTP con el detalle del problema.</returns>
    public static IActionResult ToProblem(this ControllerBase controller, Error error) {

        var statusCode = ResolveStatusCode(error.Kind);
        var type = ProblemTypes.For(error.Code);
        var title = ProblemTypes.TitleForStatus(statusCode);

        if (error.Kind != ErrorKind.Validation) {
            return controller.Problem(detail: error.Message, statusCode: statusCode, title: title, type: type);
        }

        var modelState = new ModelStateDictionary();

        if (error.FieldErrors is null) {
            // Sin campo señalado: la clave vacía es la convención de ASP.NET para los
            // errores del modelo en conjunto.
            modelState.AddModelError(string.Empty, error.Message);
        } else {
            foreach (var (field, messages) in error.FieldErrors) {
                foreach (var message in messages) {
                    modelState.AddModelError(field, message);
                }
            }
        }

        return controller.ValidationProblem(
            detail: error.FieldErrors is null ? error.Message : null,
            statusCode: statusCode,
            title: title,
            type: type,
            modelStateDictionary: modelState);

    }

    private static int ResolveStatusCode(ErrorKind kind) => kind switch {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.Unexpected => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status400BadRequest
    };

}