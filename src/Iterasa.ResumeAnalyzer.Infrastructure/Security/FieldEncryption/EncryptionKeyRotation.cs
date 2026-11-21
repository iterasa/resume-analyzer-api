using Iterasa.ResumeAnalyzer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;

/// <summary>
/// Rotación de la llave de un ámbito: la nueva versión pasa a activa y la anterior a «solo
/// descifra». Los datos no se tocan aquí: los recifra después un proceso por lotes (ADR 0015,
/// runbook de cifrado).
/// </summary>
/// <remarks>
/// Cada instancia guarda los anillos hasta <see cref="KeyRingCache.TimeToLive" />: durante ese
/// tiempo otras instancias pueden seguir escribiendo con la versión anterior, que sigue
/// descifrando. El recifrado se lanza cuando ese plazo ha pasado.
/// </remarks>
internal sealed class EncryptionKeyRotation(
    AppDbContext dbContext, EncryptionKeyProvisioner provisioner, KeyRingCache rings, TimeProvider clock) {

    /// <summary>Rota la llave de un propósito y devuelve la nueva.</summary>
    public async Task<EncryptionKeyRecord> RotateAsync(Guid scopeId, KeyPurpose purpose, CancellationToken cancellationToken) {

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var current = await dbContext.EncryptionKeys
            .SingleAsync(key => key.ScopeId == scopeId && key.Purpose == purpose && key.Status == KeyStatus.Active, cancellationToken);
        var nextVersion = await dbContext.EncryptionKeys
            .Where(key => key.ScopeId == scopeId && key.Purpose == purpose)
            .MaxAsync(key => key.Version, cancellationToken) + 1;

        // Primero se baja la activa: el índice único parcial admite una sola por propósito, y
        // EF no garantiza el orden entre una actualización y una inserción del mismo guardado.
        current.MarkDecryptOnly(clock.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        var next = await provisioner.CreateAsync(scopeId, purpose, nextVersion, cancellationToken);
        dbContext.EncryptionKeys.Add(next);
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        rings.Invalidate(scopeId);

        return next;

    }

}