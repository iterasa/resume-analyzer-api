using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Text.Json.Nodes;

namespace Company.Product.Template.Api.OpenApi;

/// <summary>
/// Añade las sondas de salud al documento.
/// </summary>
/// <remarks>
/// <c>MapHealthChecks</c> no pasa por el explorador de API, así que las sondas no
/// aparecerían. Se describen a mano: son parte del contrato con quien despliega.
/// </remarks>
internal sealed class HealthEndpointsDocumentTransformer : IOpenApiDocumentTransformer {

    private const string Tag = "Health";

    private static readonly (string Path, string Summary, string Description, bool CanBeUnavailable)[] Probes = [
        ("/health",
         "Sonda de salud general.",
         "Comprueba únicamente que el proceso responde. No consulta dependencias, así que "
         + "una caída de la base de datos no la afecta. Anónima.",
         false),
        ("/health/live",
         "Sonda de liveness.",
         "Equivalente a /health, pensada para el *liveness probe* del orquestador: si falla, "
         + "el contenedor debe reiniciarse. No depende de servicios externos a propósito.",
         false),
        ("/health/ready",
         "Sonda de readiness.",
         "Verifica la conectividad real con PostgreSQL. Devuelve 503 cuando la instancia no "
         + "puede atender tráfico. El detalle de qué dependencia falla no se publica: viaja "
         + "al log estructurado.",
         true)
    ];

    /// <inheritdoc />
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken) {

        document.Tags ??= new HashSet<OpenApiTag>();
        document.Tags.Add(new OpenApiTag { Name = Tag, Description = "Sondas para orquestadores y balanceadores." });

        document.Paths ??= [];

        foreach (var probe in Probes) {

            var responses = new OpenApiResponses {
                ["200"] = new OpenApiResponse {
                    Description = "La instancia responde correctamente.",
                    Content = CreateContent("Healthy")
                }
            };

            if (probe.CanBeUnavailable) {
                responses["503"] = new OpenApiResponse {
                    Description = "Alguna dependencia no está disponible.",
                    Content = CreateContent("Unhealthy")
                };
            }

            document.Paths[probe.Path] = new OpenApiPathItem {
                Operations = new Dictionary<HttpMethod, OpenApiOperation> {
                    [HttpMethod.Get] = new OpenApiOperation {
                        Tags = new HashSet<OpenApiTagReference> { new(Tag, document) },
                        Summary = probe.Summary,
                        Description = probe.Description,
                        Responses = responses
                    }
                }
            };

        }

        return Task.CompletedTask;

    }

    private static Dictionary<string, OpenApiMediaType> CreateContent(string exampleStatus) =>
        new(StringComparer.Ordinal) {
            ["application/json"] = new OpenApiMediaType {
                Schema = new OpenApiSchema {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) {
                        ["status"] = new OpenApiSchema {
                            Type = JsonSchemaType.String,
                            Description = "Estado agregado de la instancia.",
                            Examples = [JsonValue.Create(exampleStatus)]
                        }
                    }
                }
            }
        };

}