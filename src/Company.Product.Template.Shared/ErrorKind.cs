namespace Company.Product.Template.Shared;

/// <summary>
/// Clasificación semántica de un <see cref="Error" />. Es lo que viaja entre capas;
/// la traducción a un código de estado HTTP es responsabilidad de la capa de API,
/// de modo que el mismo caso de uso sirve a un controller, a un job o a un consumidor
/// de mensajes sin arrastrar el protocolo de transporte hasta el núcleo.
/// </summary>
public enum ErrorKind {

    /// <summary>Los datos de entrada no cumplen una regla de validación o una invariante.</summary>
    Validation,

    /// <summary>El llamante no ha demostrado su identidad o sus credenciales no son válidas.</summary>
    Unauthorized,

    /// <summary>El llamante está autenticado pero no tiene permiso sobre el recurso.</summary>
    Forbidden,

    /// <summary>El recurso solicitado no existe.</summary>
    NotFound,

    /// <summary>La operación choca con el estado actual del recurso.</summary>
    Conflict,

    /// <summary>Fallo no previsto durante la ejecución de la operación.</summary>
    Unexpected

}