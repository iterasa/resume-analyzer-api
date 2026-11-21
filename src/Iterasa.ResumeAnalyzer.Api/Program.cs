using Iterasa.ResumeAnalyzer.Api;
using Iterasa.ResumeAnalyzer.Api.Health;
using Iterasa.ResumeAnalyzer.Api.Http;
using Iterasa.ResumeAnalyzer.Api.Logging;
using Iterasa.ResumeAnalyzer.Api.OpenApi;
using Iterasa.ResumeAnalyzer.Application;
using Iterasa.ResumeAnalyzer.Infrastructure;
using Iterasa.ResumeAnalyzer.Infrastructure.Tenancy;
using Iterasa.ResumeAnalyzer.Shared.Configuration;
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

    // Las cookies son Secure fuera de desarrollo (ADR 0017): tras un proxy que termina TLS, sin
    // las cabeceras reenviadas la API ve HTTP y el antiforgery se niega a emitir tokens.
    if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing") && !ProxyHeadersExtensions.IsEnabled) {
        Log.Warning(
            "Cookies are Secure but {ForwardedHeadersVariable} is off: behind a TLS-terminating proxy every request looks like HTTP "
            + "and antiforgery tokens cannot be issued. Enable it, or terminate TLS in Kestrel.",
            EnvironmentVariables.ForwardedHeadersEnabled);
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

    // Después de CORS (un preflight no lleva credenciales) y antes de los endpoints: toda ruta
    // exige sesión salvo las declaradas anónimas (ADR 0016). La empresa de la sesión se abre
    // para el resto de la petición (ADR 0018), y el limitador va tras la autenticación para
    // particionar por usuario.
    app.UseAuthentication();
    app.UseTenantContext();
    app.UseRateLimiter();
    app.UseAuthorization();

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