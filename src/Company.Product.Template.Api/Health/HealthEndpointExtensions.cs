using Company.Product.Template.Infrastructure.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Company.Product.Template.Api.Health;

/// <summary>
/// Publicación de las sondas de salud.
/// </summary>
internal static class HealthEndpointExtensions {

    private const string HealthTag = "Health";

    /// <summary>
    /// Mapea las sondas de liveness y readiness. Son anónimas para que los orquestadores
    /// puedan consultarlas.
    /// </summary>
    /// <param name="endpoints">Constructor de rutas.</param>
    /// <returns>El mismo constructor de rutas para encadenamiento.</returns>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints) {

        var liveness = new HealthCheckOptions {
            Predicate = _ => false,
            ResponseWriter = WriteResponseAsync
        };

        var readiness = new HealthCheckOptions {
            Predicate = registration => registration.Tags.Contains(HealthCheckTags.Ready),
            ResponseWriter = WriteResponseAsync
        };

        endpoints.MapHealthChecks("/health", liveness).WithTags(HealthTag).AllowAnonymous();
        endpoints.MapHealthChecks("/health/live", liveness).WithTags(HealthTag).AllowAnonymous();
        endpoints.MapHealthChecks("/health/ready", readiness).WithTags(HealthTag).AllowAnonymous();

        return endpoints;

    }

    /// <remarks>
    /// Las sondas son anónimas: la respuesta se limita al estado agregado. Qué
    /// dependencia ha fallado y por qué solo viaja al log estructurado, que no es público.
    /// </remarks>
    private static Task WriteResponseAsync(HttpContext context, HealthReport report) {

        if (report.Status != HealthStatus.Healthy) {
            LogUnhealthyChecks(context, report);
        }

        context.Response.ContentType = "application/json; charset=utf-8";

        return context.Response.WriteAsJsonAsync(
            new { status = report.Status.ToString() },
            context.RequestAborted);

    }

    private static void LogUnhealthyChecks(HttpContext context, HealthReport report) {

        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(HealthEndpointExtensions).FullName!);

        foreach (var entry in report.Entries.Where(entry => entry.Value.Status != HealthStatus.Healthy)) {
            logger.LogWarning(
                entry.Value.Exception,
                "Health check {HealthCheckName} reported {HealthCheckStatus}: {HealthCheckDescription}",
                entry.Key,
                entry.Value.Status,
                entry.Value.Description);
        }

    }

}