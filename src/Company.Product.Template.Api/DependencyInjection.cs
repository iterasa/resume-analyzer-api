using Asp.Versioning;
using Company.Product.Template.Api.Http;
using Company.Product.Template.Api.Logging;
using Company.Product.Template.Api.OpenApi;
using Company.Product.Template.Api.Problems;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Company.Product.Template.Api;

/// <summary>
/// Registro de dependencias de la capa de API.
/// </summary>
public static class DependencyInjection {
    /// <summary>
    /// Registra los servicios de la capa de API.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <returns>La misma colección de servicios para encadenamiento.</returns>
    public static IServiceCollection AddApi(this IServiceCollection services) {

        // Único punto por el que pasa toda respuesta de error, la construya quien la
        // construya: el framework (ruta inexistente, 405, 415…), el manejador de
        // excepciones o un controller. Normaliza el tipo de problema al
        // catálogo de la API y añade los identificadores de correlación y de traza, que
        // son lo que permite encontrar en el log la petición que un cliente reporta.
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context => {

                var problem = context.ProblemDetails;
                var statusCode = problem.Status ?? context.HttpContext.Response.StatusCode;

                problem.Status ??= statusCode;

                if (!ProblemTypes.IsPublished(problem.Type)) {
                    problem.Type = ProblemTypes.ForStatus(statusCode);
                }

                problem.Title ??= ProblemTypes.TitleForStatus(statusCode);
                problem.Extensions["correlationId"] = context.HttpContext.GetCorrelationId();
                problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

            });

        // Una API que solo habla JSON: fuera el formateador de texto plano y el alias
        // heredado `text/json`, para que la documentación anuncie exactamente lo que se
        // sirve. El comodín `application/*+json` se conserva porque es el que deja salir
        // `application/problem+json`. Por eso los controllers no llevan [Produces]: ese
        // atributo sustituye el tipo de contenido de toda respuesta, incluidas las de
        // error, que dejarían de ser Problem Details.
        services
            .AddControllers(options => {
                options.OutputFormatters.RemoveType<StringOutputFormatter>();
                options.OutputFormatters.OfType<SystemTextJsonOutputFormatter>().Single().SupportedMediaTypes.Remove("text/json");
                options.Conventions.Add(new ProblemResponseContentTypeConvention());
            })
            // Los enums viajan por nombre, no por su valor numérico: legibles, estables ante
            // un reorden y así los documenta OpenAPI. Se configura también en las opciones
            // JSON del host para que el generador del documento use exactamente lo mismo.
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // La versión viaja en la ruta y todo controller la declara: no hay versión por
        // defecto que asumir, y fijar el lector evita que se busque también en cabeceras
        // y query string.
        services
            .AddApiVersioning(options => {
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
                options.ReportApiVersions = true;
            })
            .AddMvc()
            .AddApiExplorer(options => {
                options.GroupNameFormat = OpenApiExtensions.GroupNameFormat;
                options.SubstituteApiVersionInUrl = true;
            })
            .AddConfiguredOpenApi();

        services.AddHealthChecks();
        services.AddConfiguredForwardedHeaders();
        services.AddConfiguredCors();

        return services;

    }

}