using Asp.Versioning;
using Iterasa.ResumeAnalyzer.Shared;
using Iterasa.ResumeAnalyzer.Shared.Configuration;
using Serilog;
using Serilog.AspNetCore;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Iterasa.ResumeAnalyzer.Api.Logging;

/// <summary>
/// Configuración del logging estructurado de la aplicación.
/// </summary>
internal static class LoggingExtensions {

    private static readonly string ApplicationName = ApplicationInfo.Name;

    /// <summary>
    /// Plantilla de la consola en <c>LOG_FORMAT=text</c>.
    /// </summary>
    /// <remarks>
    /// Es la de Serilog sin el modificador <c>l</c> en <c>{Message}</c>: con él las cadenas
    /// salen tal cual, y una ruta con <c>%0A</c> (o cualquier otro valor del llamante)
    /// abre una línea nueva que parece un evento legítimo. Con <c>j</c> toda cadena sale
    /// entre comillas y con los caracteres de control escapados, como en el modo JSON.
    /// </remarks>
    internal const string TextOutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:j}{NewLine}{Exception}";

    /// <summary>
    /// Construye el logger raíz a partir de las variables de entorno <c>LOG_LEVEL</c> y <c>LOG_FORMAT</c>.
    /// </summary>
    /// <returns>Logger configurado.</returns>
    public static Logger CreateLogger() {

        var minimumLevel = ParseLevel(EnvironmentVariableReader.GetOptional(EnvironmentVariables.LogLevel));
        var useJson = !string.Equals(
            EnvironmentVariableReader.GetString(EnvironmentVariables.LogFormat, "json"),
            "text",
            StringComparison.OrdinalIgnoreCase);

        var configuration = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("application", ApplicationName);

        return useJson
            ? configuration.WriteTo.Console(new CompactJsonFormatter()).CreateLogger()
            : configuration.WriteTo.Console(outputTemplate: TextOutputTemplate).CreateLogger();

    }

    /// <summary>
    /// Configura el evento único por petición: método, ruta, estado, duración y contexto del llamante.
    /// </summary>
    /// <param name="options">Opciones del middleware de logging de peticiones.</param>
    public static void ConfigureRequestLogging(RequestLoggingOptions options) {

        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

        options.GetLevel = static (httpContext, elapsed, exception) =>
            exception is not null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError
                ? LogEventLevel.Error
                : httpContext.Response.StatusCode >= StatusCodes.Status400BadRequest
                    ? LogEventLevel.Warning
                    : LogEventLevel.Information;

        options.EnrichDiagnosticContext = static (diagnosticContext, httpContext) => {

            diagnosticContext.Set("CorrelationId", httpContext.GetCorrelationId());
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString());
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
            diagnosticContext.Set("UserId", httpContext.User.FindFirst("sub")?.Value);
            diagnosticContext.Set("ApiVersion", httpContext.RequestedApiVersion?.ToString());

        };

    }

    private static LogEventLevel ParseLevel(string? rawLevel) =>
        Enum.TryParse<LogEventLevel>(rawLevel, ignoreCase: true, out var level)
            ? level
            : LogEventLevel.Information;

}