using Iterasa.ResumeAnalyzer.Domain.Common;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Tenancy;

/// <summary>
/// Segunda barrera del aislamiento entre empresas: rechaza guardar una entidad de empresa que no
/// sea de la empresa de la operación, o sin empresa abierta.
/// </summary>
/// <remarks>
/// Los filtros de consulta impiden leer filas ajenas; esto impide escribirlas, por ejemplo con un
/// <c>TenantId</c> tomado del cuerpo de una petición en lugar de la sesión.
/// </remarks>
internal sealed class TenantOwnershipGuard : SaveChangesInterceptor {

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) {
        Check(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
        Check(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void Check(DbContext? context) {

        if (context is null) {
            return;
        }

        var current = TenantCryptoScope.Current;

        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>()) {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && entry.Entity.TenantId != current) {
                throw new InvalidOperationException(
                    $"{entry.Metadata.ClrType.Name} belongs to tenant {entry.Entity.TenantId}, but the operation runs in {(current is null ? "no tenant" : $"tenant {current}")}.");
            }
        }

    }

}