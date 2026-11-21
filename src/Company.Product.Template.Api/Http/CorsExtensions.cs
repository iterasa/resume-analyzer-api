using Company.Product.Template.Shared.Configuration;
using Microsoft.Extensions.Options;
using CorsOptions = Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions;

namespace Company.Product.Template.Api.Http;

/// <summary>
/// Configuración de CORS a partir de variables de entorno.
/// </summary>
/// <remarks>
/// Desactivado por defecto: una API sin cliente de navegador no debe tener política
/// alguna. Activado, exige la lista de orígenes y la valida al arrancar, de modo que un
/// origen mal escrito detiene el proceso en lugar de dejar fuera al cliente en silencio.
/// </remarks>
internal static class CorsExtensions {

    /// <summary>
    /// Registra la política CORS y su validación de arranque.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <returns>La misma colección de servicios para encadenamiento.</returns>
    public static IServiceCollection AddConfiguredCors(this IServiceCollection services) {

        services
            .AddOptions<CorsPolicyOptions>()
            .Configure(options => {
                options.Enabled = EnvironmentVariableReader.GetBool(EnvironmentVariables.CorsEnabled, false);
                options.AllowedOrigins = Split(EnvironmentVariableReader.GetOptional(EnvironmentVariables.CorsAllowedOrigins));
                options.AllowCredentials = EnvironmentVariableReader.GetBool(EnvironmentVariables.CorsAllowCredentials, false);
                options.PreflightMaxAgeSeconds = EnvironmentVariableReader.GetInt(EnvironmentVariables.CorsPreflightMaxAgeSeconds, CorsPolicyOptions.DefaultPreflightMaxAgeSeconds);
            })
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<CorsPolicyOptions>, CorsPolicyOptionsValidator>();

        services.AddCors();

        // La política del framework se construye a partir de la ya validada. Una sola,
        // la «por defecto», que `UseCors()` aplica a todos los endpoints.
        services
            .AddOptions<CorsOptions>()
            .Configure<IOptions<CorsPolicyOptions>>((cors, configured) => {

                var policy = configured.Value;

                if (!policy.Enabled) {
                    return;
                }

                var origins = policy.AllowedOrigins
                    .Select(origin => CorsOrigins.TryNormalize(origin, out var normalized) ? normalized : origin)
                    .ToArray();

                cors.AddDefaultPolicy(builder => {

                    builder
                        .WithOrigins(origins)
                        .WithMethods(CorsPolicyOptions.AllowedMethods)
                        .WithHeaders(CorsPolicyOptions.AllowedHeaders)
                        .WithExposedHeaders(CorsPolicyOptions.ExposedHeaders)
                        .SetPreflightMaxAge(TimeSpan.FromSeconds(policy.PreflightMaxAgeSeconds));

                    if (origins.Any(CorsOrigins.IsWildcardSubdomain)) {
                        builder.SetIsOriginAllowedToAllowWildcardSubdomains();
                    }

                    if (policy.AllowCredentials) {
                        builder.AllowCredentials();
                    } else {
                        builder.DisallowCredentials();
                    }

                });

            });

        return services;

    }

    /// <summary>
    /// Añade el middleware CORS al pipeline si la política está activa. Debe ir antes que
    /// cualquier middleware que pueda rechazar la petición: las cabeceras han de acompañar
    /// también a una respuesta de error.
    /// </summary>
    /// <param name="app">Pipeline de la aplicación.</param>
    /// <returns>El mismo pipeline para encadenamiento.</returns>
    public static IApplicationBuilder UseConfiguredCors(this IApplicationBuilder app) {

        var policy = app.ApplicationServices.GetRequiredService<IOptions<CorsPolicyOptions>>().Value;

        if (!policy.Enabled) {
            return app;
        }

        var logger = app.ApplicationServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(CorsExtensions).FullName!);

        logger.LogInformation(
            "CORS enabled for origins {CorsOrigins} (credentials: {CorsAllowCredentials})",
            policy.AllowedOrigins,
            policy.AllowCredentials);

        if (policy.AllowCredentials && policy.AllowedOrigins.Any(origin => CorsOrigins.TryNormalize(origin, out var normalized) && CorsOrigins.IsWildcardSubdomain(normalized))) {
            logger.LogWarning(
                "CORS allows credentials for a wildcard subdomain origin: any subdomain that an attacker controls can make credentialed requests. "
                + "Prefer listing the exact origins that need credentials.");
        }

        if (policy.AllowCredentials && policy.AllowedOrigins.Any(origin => origin.StartsWith("http://", StringComparison.OrdinalIgnoreCase))) {
            logger.LogWarning(
                "CORS allows credentials for a plain http origin: anyone on the network path can inject script into that origin and make credentialed requests. "
                + "Prefer https origins when credentials are allowed.");
        }

        return app.UseCors();

    }

    private static string[] Split(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <remarks>
    /// Los mensajes nombran la variable y el valor que falla: es lo que quien despliega
    /// necesita leer en el log para corregirlo sin abrir el código.
    /// </remarks>
    private sealed class CorsPolicyOptionsValidator : IValidateOptions<CorsPolicyOptions> {

        public ValidateOptionsResult Validate(string? name, CorsPolicyOptions options) {

            if (!options.Enabled) {
                return ValidateOptionsResult.Success;
            }

            var failures = new List<string>();

            if (options.AllowedOrigins.Count == 0) {
                failures.Add(
                    $"{EnvironmentVariables.CorsAllowedOrigins} is required when {EnvironmentVariables.CorsEnabled} is true: "
                    + "list the browser origins, comma-separated (e.g. https://app.example.com).");
            }

            var invalid = options.AllowedOrigins.Where(origin => !CorsOrigins.TryNormalize(origin, out _)).ToList();

            if (invalid.Count > 0) {
                failures.Add(
                    $"{EnvironmentVariables.CorsAllowedOrigins} contains invalid origins: {string.Join(", ", invalid.Select(origin => $"'{origin}'"))}. "
                    + "Each origin must be scheme://host[:port] (http or https) without a path, or https://*.example.com to allow every subdomain; '*' is never allowed.");
            }

            if (options.PreflightMaxAgeSeconds < 0) {
                failures.Add($"{EnvironmentVariables.CorsPreflightMaxAgeSeconds} cannot be negative.");
            }

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);

        }

    }

}