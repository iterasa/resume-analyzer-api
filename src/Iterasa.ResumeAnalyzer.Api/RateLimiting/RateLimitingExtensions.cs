using Iterasa.ResumeAnalyzer.Api.Problems;
using Iterasa.ResumeAnalyzer.Shared.Configuration;
using Iterasa.ResumeAnalyzer.Shared.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;

namespace Iterasa.ResumeAnalyzer.Api.RateLimiting;

/// <summary>
/// Configuración del limitador de peticiones.
/// </summary>
internal static class RateLimitingExtensions {

    private const string SubjectClaim = SessionClaims.UserId;

    /// <summary>
    /// Registra las políticas de limitación y su configuración desde variables de entorno.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <returns>La misma colección de servicios para encadenamiento.</returns>
    public static IServiceCollection AddConfiguredRateLimiting(this IServiceCollection services) {

        services
            .AddOptions<RateLimitingOptions>()
            .Configure(options => {
                options.Enabled = EnvironmentVariableReader.GetBool(EnvironmentVariables.RateLimitEnabled, true);
                options.PermitLimit = EnvironmentVariableReader.GetInt(EnvironmentVariables.RateLimitPermitLimit, 100);
                options.WindowSeconds = EnvironmentVariableReader.GetInt(EnvironmentVariables.RateLimitWindowSeconds, 60);
                options.AuthPermitLimit = EnvironmentVariableReader.GetInt(EnvironmentVariables.RateLimitAuthPermitLimit, 10);
                options.AuthWindowSeconds = EnvironmentVariableReader.GetInt(EnvironmentVariables.RateLimitAuthWindowSeconds, 60);
            })
            .Validate(
                options => options.PermitLimit > 0 && options.AuthPermitLimit > 0,
                $"{EnvironmentVariables.RateLimitPermitLimit} and {EnvironmentVariables.RateLimitAuthPermitLimit} must be greater than zero.")
            .Validate(
                options => options.WindowSeconds > 0 && options.AuthWindowSeconds > 0,
                $"{EnvironmentVariables.RateLimitWindowSeconds} and {EnvironmentVariables.RateLimitAuthWindowSeconds} must be greater than zero.")
            .ValidateOnStart();

        services.AddRateLimiter(limiter => {

            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.AddPolicy(RateLimitPolicies.PerClient, httpContext =>
                CreatePartition(httpContext, byClientAddress: false, options => (options.PermitLimit, options.WindowSeconds)));

            limiter.AddPolicy(RateLimitPolicies.Authentication, httpContext =>
                CreatePartition(httpContext, byClientAddress: true, options => (options.AuthPermitLimit, options.AuthWindowSeconds)));

            limiter.OnRejected = OnRejectedAsync;

        });

        return services;

    }

    /// <remarks>
    /// El limitador se ejecuta después de la autenticación, de modo que la partición natural es
    /// el usuario de la sesión. Las peticiones anónimas (inicio de sesión, registro) caen a la IP
    /// de origen: tras un proxy, solo es la del cliente con FORWARDED_HEADERS_* configurado.
    /// </remarks>
    private static RateLimitPartition<string> CreatePartition(
        HttpContext httpContext,
        bool byClientAddress,
        Func<RateLimitingOptions, (int PermitLimit, int WindowSeconds)> selector) {

        var options = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

        if (!options.Enabled) {
            return RateLimitPartition.GetNoLimiter("disabled");
        }

        var (permitLimit, windowSeconds) = selector(options);

        return RateLimitPartition.GetFixedWindowLimiter(
            $"{(byClientAddress ? RateLimitPolicies.Authentication : RateLimitPolicies.PerClient)}:{ResolvePartitionKey(httpContext, byClientAddress)}",
            _ => new FixedWindowRateLimiterOptions {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });

    }

    /// <remarks>
    /// La política de negocio cuenta por usuario. La de inicio de sesión y registro cuenta
    /// siempre por IP, aunque la petición traiga una sesión: si no, cada cuenta registrada daría
    /// una cuota nueva desde la misma IP.
    /// </remarks>
    private static string ResolvePartitionKey(HttpContext httpContext, bool byClientAddress) {

        var userId = httpContext.User.FindFirst(SubjectClaim)?.Value;

        if (!byClientAddress && !string.IsNullOrWhiteSpace(userId)) {
            return $"user:{userId}";
        }

        return $"ip:{ClientNetwork(httpContext.Connection.RemoteIpAddress)}";

    }

    /// <summary>
    /// La IP del cliente; en IPv6, su red /64: un solo cliente suele tener un /64 entero y
    /// podría rotar direcciones dentro de él para estrenar cuota.
    /// </summary>
    internal static string ClientNetwork(IPAddress? address) {

        if (address is null) {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6) {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6) {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return $"{new IPAddress(bytes)}/64";

    }

    private static async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken) {

        var httpContext = context.HttpContext;
        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)) {
            httpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        var logger = httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(RateLimitingExtensions).FullName!);

        logger.LogWarning(
            "Rate limit exceeded for {RequestMethod} {RequestPath} from partition {RateLimitPartition}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            ResolvePartitionKey(httpContext, byClientAddress: false));

        await httpContext.RequestServices
            .GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails {
                    Type = ProblemTypes.ForStatus(StatusCodes.Status429TooManyRequests),
                    Title = ProblemTypes.TitleForStatus(StatusCodes.Status429TooManyRequests),
                    Detail = "Request quota exceeded. Retry after the period indicated in the Retry-After header.",
                    Status = StatusCodes.Status429TooManyRequests
                }
            });

    }

}