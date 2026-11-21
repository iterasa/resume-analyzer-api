using Iterasa.ResumeAnalyzer.Api.Idempotency;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Iterasa.ResumeAnalyzer.Api.OpenApi;

/// <summary>
/// Documenta la cabecera <c>Idempotency-Key</c> en las operaciones marcadas con <see cref="IdempotentAttribute" />.
/// </summary>
internal sealed class IdempotencyKeyOperationTransformer : IOpenApiOperationTransformer {

    /// <inheritdoc />
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken) {

        if (!context.Description.ActionDescriptor.EndpointMetadata.OfType<IdempotentAttribute>().Any()) {
            return Task.CompletedTask;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter {
            Name = IdempotencyFilter.HeaderName,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Clave elegida por el llamante (por ejemplo, un UUID). Un reintento con la misma clave "
                + "recibe la misma respuesta que la primera petición, marcada con la cabecera "
                + $"{IdempotencyFilter.ReplayedHeaderName}: true. La misma clave con otro contenido responde 422.",
            Schema = new OpenApiSchema {
                Type = JsonSchemaType.String,
                MinLength = 1,
                MaxLength = IdempotencyFilter.MaxKeyLength
            }
        });

        return Task.CompletedTask;

    }

}