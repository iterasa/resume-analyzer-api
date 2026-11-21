namespace Company.Product.Template.Shared;

/// <summary>
/// Representa un error de aplicación serializable y transportable entre capas.
/// </summary>
/// <remarks>
/// El error lleva su clasificación semántica (<see cref="ErrorKind" />), no un código de
/// estado HTTP: el protocolo de transporte es una decisión de la capa de API.
/// Un error de validación puede señalar campos concretos con <see cref="FieldErrors" />;
/// cuando no lo hace, el mensaje describe la regla incumplida en conjunto.
/// </remarks>
/// <param name="Code">Código estable del error, único por causa. Es el identificador que consume el llamante.</param>
/// <param name="Message">Mensaje legible asociado al error.</param>
/// <param name="Kind">Clasificación semántica con la que la capa de API resuelve la respuesta.</param>
/// <param name="FieldErrors">Mensajes por campo de entrada, cuando el error señala campos concretos.</param>
public sealed record Error(string Code, string Message, ErrorKind Kind, IReadOnlyDictionary<string, string[]>? FieldErrors = null) {

    /// <summary>Código de los errores de validación, cualquiera que sea su origen.</summary>
    public const string ValidationCode = "validation_error";

    /// <summary>Mensaje resumen de un error de validación con detalle por campo.</summary>
    public const string ValidationSummary = "One or more validation errors occurred.";

    /// <summary>
    /// Crea un error de validación que describe la regla incumplida sin señalar un campo.
    /// </summary>
    /// <param name="message">Mensaje del error.</param>
    /// <returns>Instancia de <see cref="Error" />.</returns>
    public static Error Validation(string message) => new(ValidationCode, message, ErrorKind.Validation);

    /// <summary>
    /// Crea un error de validación que señala un campo concreto.
    /// </summary>
    /// <param name="field">Nombre del campo de entrada, tal y como lo conoce el llamante.</param>
    /// <param name="message">Regla que incumple el campo.</param>
    /// <returns>Instancia de <see cref="Error" />.</returns>
    public static Error Validation(string field, string message) =>
        Validation(new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] });

    /// <summary>
    /// Crea un error de validación con mensajes por campo.
    /// </summary>
    /// <param name="fieldErrors">Mensajes por campo de entrada.</param>
    /// <returns>Instancia de <see cref="Error" />.</returns>
    public static Error Validation(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new(ValidationCode, ValidationSummary, ErrorKind.Validation, fieldErrors);

    /// <summary>
    /// Crea un error de autenticación.
    /// </summary>
    /// <param name="message">Mensaje del error.</param>
    /// <returns>Instancia de <see cref="Error" />.</returns>
    public static Error Unauthorized(string message) => new("unauthorized", message, ErrorKind.Unauthorized);

    /// <summary>
    /// Crea un error de autorización.
    /// </summary>
    /// <param name="message">Mensaje del error.</param>
    /// <returns>Instancia de <see cref="Error" />.</returns>
    public static Error Forbidden(string message) => new("forbidden", message, ErrorKind.Forbidden);

    /// <summary>
    /// Crea un error de recurso no encontrado.
    /// </summary>
    /// <param name="message">Mensaje del error.</param>
    /// <returns>Instancia de <see cref="Error" />.</returns>
    public static Error NotFound(string message) => new("not_found", message, ErrorKind.NotFound);

    /// <summary>
    /// Crea un error de conflicto.
    /// </summary>
    /// <param name="message">Mensaje del error.</param>
    /// <returns>Instancia de <see cref="Error" />.</returns>
    public static Error Conflict(string message) => new("conflict", message, ErrorKind.Conflict);

    /// <summary>
    /// Crea un error inesperado.
    /// </summary>
    /// <param name="message">Mensaje del error.</param>
    /// <returns>Instancia de <see cref="Error" />.</returns>
    public static Error Unexpected(string message) => new("unexpected_error", message, ErrorKind.Unexpected);

}