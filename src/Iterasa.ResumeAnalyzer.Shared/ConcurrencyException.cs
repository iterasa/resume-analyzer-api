namespace Iterasa.ResumeAnalyzer.Shared;

/// <summary>
/// Otra petición escribió entre la lectura y la escritura: modificó el mismo registro o
/// guardó uno que choca con él.
/// </summary>
/// <remarks>
/// La infraestructura la lanza al detectar el conflicto optimista o la violación de un
/// índice único, para que la capa de API responda 409 en lugar de 500: el llamante debe
/// releer y reintentar, y no es un fallo del servidor.
/// </remarks>
public sealed class ConcurrencyException(string message, Exception innerException) : Exception(message, innerException) {
}