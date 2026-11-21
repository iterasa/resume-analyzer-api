using Company.Product.Template.Api;
using Company.Product.Template.Api.Health;
using Company.Product.Template.Api.Http;
using Company.Product.Template.Api.Logging;
using Company.Product.Template.Api.OpenApi;
using Company.Product.Template.Application;
using Company.Product.Template.Infrastructure;
using Company.Product.Template.Shared.Configuration;
using Serilog;

Log.Logger = LoggingExtensions.CreateLogger();

try {

    var builder = WebApplication.CreateBuilder(args);

    builder.WebHost.ConfigureKestrel(options => {
        // La cabecera Server delata el servidor y su versión sin aportar nada al cliente.
        options.AddServerHeader = false;
        // Una API JSON no necesita el límite por defecto de 30 MB: acotarlo corta en la
        // entrada un cuerpo desmedido antes de que nadie intente deserializarlo.
        options.Limits.MaxRequestBodySize = EnvironmentVariableReader.GetInt(EnvironmentVariables.HttpMaxRequestBodyBytes, DefaultMaxRequestBodyBytes);
    });

    builder.Services.AddSerilog();
    builder.Services.AddHttpContextAccessor();

    builder.Services
        .AddApi()
        .AddApplication()
        .AddInfrastructure();

    var app = builder.Build();

    // Debe ir primero: reescribe IP y esquema antes de que los lea cualquier otro middleware.
    if (ProxyHeadersExtensions.IsEnabled) {

        app.UseForwardedHeaders();

        if (ProxyHeadersExtensions.TrustsAnyProxy) {
            Log.Warning(
                "Forwarded headers are enabled without known proxies or networks: "
                + "any caller can spoof its client IP. Configure {KnownProxiesVariable} or {KnownNetworksVariable}.",
                EnvironmentVariables.ForwardedHeadersKnownProxies,
                EnvironmentVariables.ForwardedHeadersKnownNetworks);
        }

    }

    // Las cabeceras de seguridad van fuera del manejador de excepciones para que también
    // las lleve una respuesta de error. Este envuelve al resto del pipeline; la
    // correlación se establece antes del logging para que todo evento la lleve.
    app.UseSecurityHeaders();
    app.UseGlobalExceptionHandler();
    app.UseStatusCodePages();
    app.UseCorrelationId();
    app.UseSerilogRequestLogging(LoggingExtensions.ConfigureRequestLogging);

    app.UseConfiguredOpenApi();

    // CORS antes que el resto del pipeline de la petición: sus cabeceras han de ir
    // también en las respuestas de error. Sin política activa no añade nada.
    app.UseConfiguredCors();

    app.MapControllers();
    app.MapHealthEndpoints();

    await app.RunAsync();

} catch (Exception exception) when (exception is not HostAbortedException
    && exception.GetType().Name is not "StopTheHostException") {
    // El filtro deja pasar la excepción con la que el host de pruebas detiene el arranque.
    Log.Fatal(exception, "Application terminated unexpectedly");
    throw;
} finally {
    await Log.CloseAndFlushAsync();
}

/// <summary>
/// Marca del punto de entrada expuesta para pruebas de integración.
/// </summary>
public partial class Program {

    /// <summary>Límite por defecto del cuerpo de una petición: 1 MiB.</summary>
    private const int DefaultMaxRequestBodyBytes = 1024 * 1024;

}