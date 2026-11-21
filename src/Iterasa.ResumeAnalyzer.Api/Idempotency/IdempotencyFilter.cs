using Iterasa.ResumeAnalyzer.Api.Extensions;
using Iterasa.ResumeAnalyzer.Api.Problems;
using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Options;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Iterasa.ResumeAnalyzer.Api.Idempotency;

/// <summary>
/// Ejecuta una acción una sola vez por clave de idempotencia y reproduce su respuesta en los reintentos.
/// </summary>
/// <remarks>
/// Sigue el borrador <c>draft-ietf-httpapi-idempotency-key-header</c>: la clave la elige el
/// llamante y se acota a su empresa y su identidad, de modo que dos llamantes no se pisan; una clave
/// reutilizada con otro contenido es un error del llamante (422), y dos peticiones
/// simultáneas con la misma clave se resuelven en la base de datos: solo una confirma y
/// la otra reproduce su respuesta. Las respuestas de error (4xx) también se registran:
/// un reintento debe recibir lo mismo que la primera vez, sea lo que sea.
/// </remarks>
internal sealed class IdempotencyFilter(
    IIdempotencyStore store,
    ICurrentPrincipal currentPrincipal,
    TimeProvider clock,
    IOptions<IdempotencyOptions> options,
    IOptions<MvcOptions> mvcOptions,
    IUrlHelperFactory urlHelperFactory,
    ProblemDetailsFactory problemDetailsFactory,
    ILogger<IdempotencyFilter> logger) : IAsyncActionFilter {

    /// <summary>Cabecera con la que el llamante identifica la operación.</summary>
    public const string HeaderName = "Idempotency-Key";

    /// <summary>Cabecera que marca una respuesta reproducida en lugar de ejecutada.</summary>
    public const string ReplayedHeaderName = "Idempotency-Replayed";

    /// <summary>Longitud máxima de la clave. Un UUID tiene 36.</summary>
    public const int MaxKeyLength = 128;

    private const string KeyReusedCode = "idempotency_key_reused";

    /// <remarks>
    /// Las del formateador que escribe la respuesta, no las de <c>JsonOptions</c>: MVC les cambia
    /// el codificador (no escapa los caracteres no ASCII), y con otras la reproducción no sería
    /// byte a byte («Inglés» frente a «Ingl\u00E9s»).
    /// </remarks>
    private JsonSerializerOptions SerializerOptions =>
        mvcOptions.Value.OutputFormatters.OfType<SystemTextJsonOutputFormatter>().First().SerializerOptions;
    private const string ConflictCode = "idempotency_conflict";

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next) {

        var request = context.HttpContext.Request;

        if (!request.Headers.TryGetValue(HeaderName, out var values)) {
            await next();
            return;
        }

        var key = values.ToString();

        if (!IsValidKey(key)) {
            context.Result = ((ControllerBase)context.Controller).ToProblem(
                Error.Validation(HeaderName, $"The {HeaderName} header must be 1 to {MaxKeyLength} visible ASCII characters."));
            return;
        }

        // Acotada a la empresa y al usuario: dos personas, o la misma en dos empresas, no
        // comparten claves. Sin sesión no hay idempotencia (un ámbito «anónimo» compartido
        // dejaría a un tercero reproducir la respuesta de otro).
        if (!currentPrincipal.IsAuthenticated || currentPrincipal.TenantId == Guid.Empty) {
            await next();
            return;
        }

        var scope = $"t:{currentPrincipal.TenantId:N}:u:{currentPrincipal.UserId:N}";
        var requestHash = ComputeRequestHash(request.Method, request.Path, context.ActionArguments);
        var now = clock.GetUtcNow();
        var cancellationToken = context.HttpContext.RequestAborted;

        var existing = await store.FindAsync(scope, key, now, cancellationToken);

        if (existing is not null) {
            context.Result = Resolve(context, existing, requestHash);
            return;
        }

        ActionExecutedContext? executed = null;

        var recorded = await store.RunAndRecordAsync(
            scope,
            key,
            requestHash,
            async () => {
                executed = await next();
                if (executed.Exception is not null && !executed.ExceptionHandled) {
                    // Una excepción no se registra: la transacción se deshace y el reintento
                    // vuelve a ejecutar. Se relanza para que la trate el manejador global.
                    ExceptionDispatchInfo.Capture(executed.Exception).Throw();
                }
                return Capture(executed);
            },
            now,
            options.Value.Retention,
            cancellationToken);

        if (recorded is not null) {
            return;
        }

        // Otra petición con la misma clave confirmó antes: lo que hizo esta se ha deshecho
        // y se responde con lo que registró la ganadora.
        // Lo que hizo esta petición se deshizo, también lo que su caso de uso dejó en la auditoría
        // como hecho: este evento lo compensa.
        logger.LogWarning(
            "Audit {AuditAction} on {AuditTarget} by {ActorId}: {AuditOutcome} ({AuditReason})",
            "idempotency.race",
            $"{request.Method} {request.Path}",
            currentPrincipal.UserId,
            "rolled_back",
            "another request with the same Idempotency-Key committed first; this one's changes and audit entries were undone");

        var winner = await store.FindAsync(scope, key, now, cancellationToken);

        executed!.Result = winner is null
            ? Problem(context, StatusCodes.Status409Conflict, ConflictCode, "Another request with the same Idempotency-Key is being processed. Retry shortly.")
            : Resolve(context, winner, requestHash);

    }

    private IActionResult Resolve(ActionContext context, IdempotencyRecord record, string requestHash) {

        if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal)) {
            return Problem(
                context,
                StatusCodes.Status422UnprocessableEntity,
                KeyReusedCode,
                $"The {HeaderName} was already used for a different request. Use a new key for a new operation.");
        }

        var headers = context.HttpContext.Response.Headers;
        headers[ReplayedHeaderName] = "true";

        if (record.Response.Location is not null) {
            headers.Location = record.Response.Location;
        }

        return record.Response.Body is null
            ? new StatusCodeResult(record.Response.StatusCode)
            : new ContentResult {
                StatusCode = record.Response.StatusCode,
                ContentType = record.Response.ContentType,
                Content = record.Response.Body
            };

    }

    /// <remarks>
    /// Se captura el resultado de la acción, no la respuesta escrita: a esta altura del
    /// pipeline el cuerpo aún no existe. Se serializa con las mismas opciones que usará
    /// MVC, de modo que la reproducción es byte a byte lo que habría salido.
    /// </remarks>
    private IdempotentResponse Capture(ActionExecutedContext executed) => executed.Result switch {

        ObjectResult objectResult => new IdempotentResponse(
            objectResult.StatusCode ?? StatusCodes.Status200OK,
            ResolveContentType(objectResult),
            objectResult.Value is null ? null : JsonSerializer.Serialize(objectResult.Value, objectResult.Value.GetType(), SerializerOptions),
            objectResult is CreatedAtActionResult created ? ResolveLocation(executed, created) : null),

        StatusCodeResult statusCodeResult => new IdempotentResponse(statusCodeResult.StatusCode, null, null, null),

        null => new IdempotentResponse(StatusCodes.Status200OK, null, null, null),

        var other => throw new NotSupportedException(
            $"[{nameof(IdempotentAttribute)}] cannot record a {other.GetType().Name}; return an ObjectResult or a StatusCodeResult.")

    };

    /// <remarks>
    /// <c>Problem()</c> devuelve un <see cref="ObjectResult" /> sin tipo de contenido: el
    /// <c>application/problem+json</c> lo infiere MVC al ejecutar el resultado, que aún no
    /// ha ocurrido. Se anticipa aquí la misma regla.
    /// </remarks>
    private static string ResolveContentType(ObjectResult result) =>
        result.ContentTypes.FirstOrDefault()
            ?? (result.Value is ProblemDetails ? ProblemResponseContentTypeConvention.ContentType : "application/json");

    private string? ResolveLocation(ActionContext context, CreatedAtActionResult created) {

        var request = context.HttpContext.Request;

        return urlHelperFactory
            .GetUrlHelper(context)
            .Action(created.ActionName, created.ControllerName, created.RouteValues, request.Scheme, request.Host.ToUriComponent());

    }

    private ObjectResult Problem(ActionContext context, int statusCode, string code, string detail) {

        var problem = problemDetailsFactory.CreateProblemDetails(
            context.HttpContext,
            statusCode: statusCode,
            title: ProblemTypes.TitleForStatus(statusCode),
            type: ProblemTypes.For(code),
            detail: detail);

        return new ObjectResult(problem) {
            StatusCode = statusCode,
            ContentTypes = { ProblemResponseContentTypeConvention.ContentType }
        };

    }

    /// <remarks>
    /// La huella cubre método, ruta y los argumentos ya enlazados —no el cuerpo crudo—, de
    /// modo que dos serializaciones distintas del mismo contenido cuentan como la misma
    /// petición. El token de cancelación no forma parte de la petición.
    /// </remarks>
    private string ComputeRequestHash(string method, PathString path, IDictionary<string, object?> arguments) {

        var canonical = arguments
            .Where(argument => argument.Value is not CancellationToken)
            .OrderBy(argument => argument.Key, StringComparer.Ordinal)
            .ToDictionary(argument => argument.Key, argument => argument.Value, StringComparer.Ordinal);

        var payload = $"{method}\n{path}\n{JsonSerializer.Serialize(canonical, SerializerOptions)}";

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

    }

    private static bool IsValidKey(string key) =>
        key.Length is > 0 and <= MaxKeyLength && key.All(character => character is > ' ' and <= '~');

}