using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence;

/// <summary>
/// Registro de operaciones idempotentes sobre la tabla <c>idempotency_key</c>.
/// </summary>
internal sealed class IdempotencyStore(AppDbContext dbContext) : IIdempotencyStore {

    /// <inheritdoc />
    public async Task<IdempotencyRecord?> FindAsync(string scope, string key, DateTimeOffset now, CancellationToken cancellationToken = default) {

        var row = await dbContext.IdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Scope == scope && k.Key == key && k.ExpiresAtUtc > now, cancellationToken);

        return row is null ? null : ToRecord(row);

    }

    /// <remarks>
    /// La operación y el registro comparten transacción: la operación escribe a través
    /// del mismo contexto, así que su <c>SaveChanges</c> queda dentro. Una clave ya
    /// registrada hace fallar la inserción por la clave primaria; entonces se deshace todo
    /// y el llamante reproduce la respuesta que ganó. Un registro caducado con la misma
    /// clave se retira antes, para que la clave pueda reutilizarse.
    /// </remarks>
    public async Task<IdempotencyRecord?> RunAndRecordAsync(
        string scope,
        string key,
        string requestHash,
        Func<Task<IdempotentResponse>> operation,
        DateTimeOffset now,
        TimeSpan retention,
        CancellationToken cancellationToken = default) {

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.IdempotencyKeys
            .Where(k => k.Scope == scope && k.Key == key && k.ExpiresAtUtc <= now)
            .ExecuteDeleteAsync(cancellationToken);

        var response = await operation();

        var row = new IdempotencyKey {
            Scope = scope,
            Key = key,
            RequestHash = requestHash,
            StatusCode = response.StatusCode,
            ContentType = response.ContentType,
            ResponseBody = response.Body,
            Location = response.Location,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(retention)
        };

        dbContext.IdempotencyKeys.Add(row);

        try {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        } catch (DbUpdateException) {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        return ToRecord(row);

    }

    /// <inheritdoc />
    public Task<int> PurgeExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        dbContext.IdempotencyKeys.Where(k => k.ExpiresAtUtc <= now).ExecuteDeleteAsync(cancellationToken);

    private static IdempotencyRecord ToRecord(IdempotencyKey row) => new(
        row.Scope,
        row.Key,
        row.RequestHash,
        new IdempotentResponse(row.StatusCode, row.ContentType, row.ResponseBody, row.Location),
        row.CreatedAtUtc,
        row.ExpiresAtUtc);

}