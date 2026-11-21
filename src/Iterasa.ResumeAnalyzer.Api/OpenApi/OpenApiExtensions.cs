using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Asp.Versioning.OpenApi.Transformers;
using Iterasa.ResumeAnalyzer.Shared;
using Iterasa.ResumeAnalyzer.Shared.Configuration;

namespace Iterasa.ResumeAnalyzer.Api.OpenApi;

/// <summary>
/// Generación nativa del documento OpenAPI (un documento por versión) y su interfaz.
/// </summary>
/// <remarks>
/// El documento lo genera ASP.NET Core (<c>Microsoft.AspNetCore.OpenApi</c>) y la
/// integración de <c>Asp.Versioning.OpenApi</c> crea uno por cada versión que declaran los
/// controllers. Swagger UI es solo la interfaz y podría sustituirse por cualquier otra
/// que lea <c>/openapi/{v}.json</c>.
/// </remarks>
internal static class OpenApiExtensions {

    /// <summary>Formato del nombre de grupo y de documento: <c>v1</c>, <c>v2</c>…</summary>
    public const string GroupNameFormat = "'v'VVV";

    private const string DocumentRoute = "/openapi/{documentName}.json";

    /// <summary>Indica si la documentación se expone. Desactivarla en producción es lo habitual.</summary>
    public static bool IsEnabled => EnvironmentVariableReader.GetBool(EnvironmentVariables.SwaggerEnabled, true);

    /// <summary>
    /// Registra un documento por versión de la API, con sus transformaciones.
    /// </summary>
    /// <remarks>
    /// Los comentarios XML del ensamblado de entrada (controllers y contratos) los aplica la
    /// integración sola. Los de <c>Application</c> y <c>Shared</c>, donde viven los DTO y
    /// la paginación que salen en las respuestas, hay que entregárselos: sin ellos esos
    /// esquemas no tendrían descripción.
    /// </remarks>
    /// <param name="builder">Constructor del versionado.</param>
    /// <returns>El mismo constructor para encadenamiento.</returns>
    public static IApiVersioningBuilder AddConfiguredOpenApi(this IApiVersioningBuilder builder) =>
        builder.AddOpenApi(options => {

            options.Document.AddDocumentTransformer(new DocumentInfoTransformer(options.Description));
            options.Document.AddDocumentTransformer<HealthEndpointsDocumentTransformer>();

            var documentation = DocumentedAssemblies
                .Select(assembly => Path.Combine(AppContext.BaseDirectory, $"{ApplicationInfo.Name}.{assembly}.xml"))
                .Where(File.Exists)
                .ToList();

            foreach (var file in documentation.Where(file => !file.EndsWith($".{EntryAssemblySuffix}.xml", StringComparison.Ordinal))) {
                var comments = new XmlCommentsTransformer(file);
                options.Document.AddDocumentTransformer(comments);
                options.Document.AddOperationTransformer(comments);
                options.Document.AddSchemaTransformer(comments);
            }

            options.Document.AddSchemaTransformer(new RenamedPropertyDescriptionTransformer([.. documentation.Select(XmlComments.FromFile)]));

        });

    /// <remarks>El de entrada va primero: es el que la integración carga sola.</remarks>
    private const string EntryAssemblySuffix = "Api";

    private static readonly string[] DocumentedAssemblies = [EntryAssemblySuffix, "Application", "Shared"];

    /// <summary>
    /// Publica los documentos en <c>/openapi/{version}.json</c> y la interfaz en <c>/swagger</c>.
    /// </summary>
    /// <param name="app">Aplicación web.</param>
    /// <returns>La misma aplicación para encadenamiento.</returns>
    public static WebApplication UseConfiguredOpenApi(this WebApplication app) {

        if (!IsEnabled) {
            return app;
        }

        // Anónimo a propósito: solo se publica con SWAGGER_ENABLED (apagado en la imagen).
        app.MapOpenApi(DocumentRoute).WithDocumentPerVersion().AllowAnonymous();

        var versions = app.Services.GetRequiredService<IApiVersionDescriptionProvider>().ApiVersionDescriptions;

        app.UseSwaggerUI(ui => {
            ui.RoutePrefix = "swagger";
            foreach (var version in versions) {
                ui.SwaggerEndpoint(DocumentRoute.Replace("{documentName}", version.GroupName, StringComparison.Ordinal), version.GroupName.ToUpperInvariant());
            }
        });

        return app;

    }

}