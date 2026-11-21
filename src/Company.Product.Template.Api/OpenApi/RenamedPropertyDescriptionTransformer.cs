using Asp.Versioning.OpenApi.Transformers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Reflection;

namespace Company.Product.Template.Api.OpenApi;

/// <summary>
/// Describe las propiedades cuyo nombre JSON no coincide con el del miembro C#.
/// </summary>
/// <remarks>
/// La integración resuelve el comentario de una propiedad por su nombre JSON, así que
/// <c>access_token</c> no encuentra el de <c>AccessToken</c>. Aquí se resuelve por el
/// miembro real que System.Text.Json conoce, solo cuando la propiedad sigue sin
/// descripción: no pisa nada de lo que la integración ya haya puesto.
/// </remarks>
internal sealed class RenamedPropertyDescriptionTransformer(IReadOnlyList<XmlComments> comments) : IOpenApiSchemaTransformer {

    /// <inheritdoc />
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken) {

        if (!string.IsNullOrEmpty(schema.Description) || context.JsonPropertyInfo?.AttributeProvider is not MemberInfo member) {
            return Task.CompletedTask;
        }

        var summary = comments
            .Select(source => source.GetSummary(member))
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

        if (summary is not null) {
            schema.Description = summary;
        }

        return Task.CompletedTask;

    }

}