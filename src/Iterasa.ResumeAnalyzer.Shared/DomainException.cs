namespace Iterasa.ResumeAnalyzer.Shared;

/// <summary>
/// Violación de una invariante del dominio.
/// </summary>
/// <remarks>
/// El dominio protege sus invariantes lanzando, no devolviendo: un agregado en estado
/// inválido no debe llegar a existir. Este tipo propio sustituye a
/// <see cref="ArgumentException" /> para que la traducción a respuesta HTTP pueda
/// distinguir una regla de negocio incumplida de un error de programación, que debe
/// seguir aflorando como fallo inesperado. Vive en <c>Shared</c> porque lo lanza el
/// dominio y lo traduce la capa de API, que no puede referenciar al dominio.
/// </remarks>
public sealed class DomainException : Exception {

    /// <summary>
    /// Inicializa una nueva instancia de la clase <see cref="DomainException" />.
    /// </summary>
    /// <param name="message">Descripción de la invariante incumplida. Es apta para el llamante: no debe contener estado interno.</param>
    public DomainException(string message) : base(message) {
    }

    /// <summary>
    /// Inicializa una nueva instancia de la clase <see cref="DomainException" />.
    /// </summary>
    /// <param name="message">Descripción de la invariante incumplida.</param>
    /// <param name="innerException">Excepción que originó el fallo.</param>
    public DomainException(string message, Exception innerException) : base(message, innerException) {
    }

}