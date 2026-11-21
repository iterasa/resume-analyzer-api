using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Company.Product.Template.Api.Problems;

/// <summary>
/// Documenta en OpenAPI que toda respuesta de error es <c>application/problem+json</c>.
/// </summary>
/// <remarks>
/// En tiempo de ejecución ya es así: <c>Problem()</c>, <c>ValidationProblem()</c> y el
/// manejador global escriben con ese tipo. Pero <c>[ProducesResponseType]</c> sin tipo de
/// contenido hereda el del formateador, <c>application/json</c>, y la documentación
/// mentiría. Declararlo en cada atributo es ruido repetido veinte veces; esta convención
/// lo hace una vez, para todo error (4xx y 5xx) que un endpoint declare con cuerpo.
/// </remarks>
internal sealed class ProblemResponseContentTypeConvention : IControllerModelConvention {

    /// <summary>Tipo de contenido de un Problem Details (RFC 9457).</summary>
    public const string ContentType = "application/problem+json";

    /// <inheritdoc />
    public void Apply(ControllerModel controller) {

        Rewrite(controller.Filters);

        foreach (var action in controller.Actions) {
            Rewrite(action.Filters);
        }

    }

    private static void Rewrite(IList<IFilterMetadata> filters) {
        for (var index = 0; index < filters.Count; index++) {
            if (filters[index] is ProducesResponseTypeAttribute { StatusCode: >= StatusCodes.Status400BadRequest } declared
                && declared.Type != typeof(void)) {
                filters[index] = new ProducesResponseTypeAttribute(declared.Type, declared.StatusCode, ContentType);
            }
        }
    }

}