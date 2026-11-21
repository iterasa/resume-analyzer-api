using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence;

/// <summary>
/// Confirma los cambios de la petición como una transacción.
/// </summary>
internal sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork {

    /// <remarks>
    /// El conflicto optimista es un resultado esperable, no un fallo: se traduce a una
    /// excepción propia para que la API responda 409 y no 500. Lo mismo la violación de
    /// un índice único, que es como se manifiesta la carrera entre dos altas que pasaron
    /// a la vez la comprobación previa de duplicados.
    /// </remarks>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) {
        try {
            return await dbContext.SaveChangesAsync(cancellationToken);
        } catch (DbUpdateConcurrencyException exception) {
            throw new ConcurrencyException("The record was modified by another request. Reload and retry.", exception);
        } catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) {
            throw new ConcurrencyException("The record conflicts with one saved by another request. Reload and retry.", exception);
        }
    }

}