using Asp.Versioning.ApiExplorer;
using Iterasa.ResumeAnalyzer.Shared;
using Iterasa.ResumeAnalyzer.Shared.Configuration;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Iterasa.ResumeAnalyzer.Api.OpenApi;

/// <summary>
/// Identidad del documento: título, versión y descripción.
/// </summary>
internal sealed class DocumentInfoTransformer(ApiVersionDescription version) : IOpenApiDocumentTransformer {

    /// <inheritdoc />
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken) {

        var description = EnvironmentVariableReader.GetString(
            EnvironmentVariables.ApiDescription,
            "API REST versionada por ruta; los errores siguen Problem Details (RFC 9457).");

        if (version.IsDeprecated) {
            description += " Esta versión de la API está obsoleta.";
        }

        document.Info = new OpenApiInfo {
            Title = EnvironmentVariableReader.GetString(EnvironmentVariables.ApiTitle, ApplicationInfo.Name),
            Version = version.ApiVersion.ToString(),
            Description = description
        };

        return Task.CompletedTask;

    }

}