namespace Iterasa.ResumeAnalyzer.Application.Abstractions;

/// <summary>
/// Registro de las respuestas de operaciones idempotentes, por clave y por llamante.
/// </summary>
/// <remarks>
/// Un integrador que reintenta un <c>POST</c> tras un corte de red no sabe si la primera
/// petición llegó. Con una clave de idempotencia, la segunda recibe la misma respuesta que
/// la primera en lugar de crear un duplicado o un 409. El registro es compartido entre
/// instancias porque vive en la base de datos: un reintento que cae en otro nodo también
/// encuentra la respuesta.
/// </remarks>
public interface IIdempotencyStore {

    /// <summary>
    /// Busca la respuesta registrada para una clave. Nulo si no existe o ya caducó.
    /// </summary>
    /// <param name="scope">Ámbito de la clave: el llamante. Dos llamantes no comparten claves.</param>
    /// <param name="key">Clave elegida por el llamante.</param>
    /// <param name="now">Instante actual, para descartar registros caducados.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Registro encontrado o <see langword="null" />.</returns>
    Task<IdempotencyRecord?> FindAsync(string scope, string key, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta la operación y registra su respuesta como una única transacción.
    /// </summary>
    /// <remarks>
    /// Si dos peticiones con la misma clave se ejecutan a la vez, solo una confirma: la
    /// otra se deshace entera —también lo que la operación hubiera escrito— y recibe
    /// <see langword="null" />, para que el llamante busque y devuelva la respuesta ganadora.
    /// </remarks>
    /// <param name="scope">Ámbito de la clave.</param>
    /// <param name="key">Clave elegida por el llamante.</param>
    /// <param name="requestHash">Huella de la petición, con la que se detecta una clave reutilizada con otro contenido.</param>
    /// <param name="operation">Operación a ejecutar. Su respuesta es lo que se registra.</param>
    /// <param name="now">Instante actual.</param>
    /// <param name="retention">Tiempo durante el que la respuesta se conserva y se reproduce.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>El registro confirmado, o <see langword="null" /> si otra petición con la misma clave ganó.</returns>
    Task<IdempotencyRecord?> RunAndRecordAsync(
        string scope,
        string key,
        string requestHash,
        Func<Task<IdempotentResponse>> operation,
        DateTimeOffset now,
        TimeSpan retention,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina los registros caducados.
    /// </summary>
    /// <param name="now">Instante actual.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Número de registros eliminados.</returns>
    Task<int> PurgeExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

}

/// <summary>Respuesta de una operación idempotente, tal y como se reproduce en un reintento.</summary>
/// <param name="StatusCode">Código de estado HTTP.</param>
/// <param name="ContentType">Tipo de contenido del cuerpo, si lo hay.</param>
/// <param name="Body">Cuerpo serializado, si lo hay.</param>
/// <param name="Location">Cabecera <c>Location</c>, si la hubo.</param>
public sealed record IdempotentResponse(int StatusCode, string? ContentType, string? Body, string? Location);

/// <summary>Registro de una operación idempotente ya ejecutada.</summary>
/// <param name="Scope">Ámbito de la clave.</param>
/// <param name="Key">Clave elegida por el llamante.</param>
/// <param name="RequestHash">Huella de la petición original.</param>
/// <param name="Response">Respuesta registrada.</param>
/// <param name="CreatedAtUtc">Instante de la ejecución original.</param>
/// <param name="ExpiresAtUtc">Instante a partir del cual la clave puede reutilizarse.</param>
public sealed record IdempotencyRecord(
    string Scope,
    string Key,
    string RequestHash,
    IdempotentResponse Response,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);