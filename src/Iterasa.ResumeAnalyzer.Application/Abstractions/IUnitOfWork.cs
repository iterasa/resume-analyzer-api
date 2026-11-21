namespace Iterasa.ResumeAnalyzer.Application.Abstractions;

/// <summary>
/// Confirma como una sola transacción todos los cambios acumulados durante un caso de uso.
/// </summary>
/// <remarks>
/// Los repositorios registran intenciones; el commit lo decide el caso de uso, que es
/// quien conoce el límite de la operación. Sin esta separación, un comando que toca dos
/// agregados persiste el primero y puede fallar en el segundo, dejando estado a medias.
/// </remarks>
internal interface IUnitOfWork {

    /// <summary>
    /// Persiste los cambios pendientes.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Número de filas afectadas.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

}