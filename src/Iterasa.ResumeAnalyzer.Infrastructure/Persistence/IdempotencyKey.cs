namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence;

/// <summary>
/// Fila de la tabla <c>idempotency_key</c>: la respuesta registrada de una operación idempotente.
/// </summary>
/// <remarks>
/// No es una entidad del dominio sino un registro técnico de la capa HTTP, y por eso vive
/// en infraestructura: no tiene invariantes, solo se escribe una vez y se lee en los
/// reintentos hasta que caduca.
/// </remarks>
internal sealed class IdempotencyKey {

    public const int MaxScopeLength = 100;
    public const int MaxKeyLength = 128;
    public const int MaxRequestHashLength = 64;
    public const int MaxContentTypeLength = 128;
    public const int MaxLocationLength = 2048;

    public string Scope { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string RequestHash { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    public string? ContentType { get; set; }

    public string? ResponseBody { get; set; }

    public string? Location { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

}