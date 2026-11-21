using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Domain.Users;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using Microsoft.EntityFrameworkCore;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence;

/// <summary>
/// Lado de escritura del aggregate User.
/// </summary>
/// <remarks>
/// El correo está cifrado: se busca por su índice ciego, con cada versión vigente de la llave.
/// </remarks>
internal sealed class UserRepository(AppDbContext dbContext, BlindIndexer blindIndexer) : IUserRepository {

    /// <inheritdoc />
    public void Add(User user) => dbContext.Users.Add(user);

    /// <inheritdoc />
    public async Task<bool> ExistsWithEmailAsync(string email, CancellationToken cancellationToken = default) {
        var hashes = await EmailHashesAsync(email, cancellationToken);
        return await dbContext.Users.AnyAsync(
            u => hashes.Contains(EF.Property<byte[]>(u.Email, EncryptedFields.UserEmail.BlindIndexProperty)),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) {
        var hashes = await EmailHashesAsync(email, cancellationToken);
        return await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(
            u => hashes.Contains(EF.Property<byte[]>(u.Email, EncryptedFields.UserEmail.BlindIndexProperty)),
            cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Dos sentencias, cada una atómica: la suma no pierde intentos simultáneos, y el bloqueo salta
    /// en cuanto la cuenta llega al máximo, lo haya sumado esta petición u otra.
    /// </remarks>
    public async Task<bool> RecordFailedSignInAsync(Guid userId, int maxAttempts, TimeSpan lockoutDuration, DateTimeOffset now, CancellationToken cancellationToken = default) {

        await dbContext.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.FailedSignInAttempts, u => u.FailedSignInAttempts + 1), cancellationToken);

        var lockedUntil = now.Add(lockoutDuration);

        var locked = await dbContext.Users
            .Where(u => u.Id == userId && u.FailedSignInAttempts >= maxAttempts)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.LockedUntilUtc, lockedUntil)
                .SetProperty(u => u.FailedSignInAttempts, 0), cancellationToken);

        return locked > 0;

    }

    /// <inheritdoc />
    public async Task RecordSuccessfulSignInAsync(Guid userId, string? upgradedPasswordHash, CancellationToken cancellationToken = default) {

        var user = dbContext.Users.Where(u => u.Id == userId);

        if (upgradedPasswordHash is null) {
            await user.ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.FailedSignInAttempts, 0)
                .SetProperty(u => u.LockedUntilUtc, (DateTimeOffset?)null), cancellationToken);
            return;
        }

        await user.ExecuteUpdateAsync(setters => setters
            .SetProperty(u => u.FailedSignInAttempts, 0)
            .SetProperty(u => u.LockedUntilUtc, (DateTimeOffset?)null)
            .SetProperty(u => u.PasswordHash, upgradedPasswordHash), cancellationToken);

    }

    /// <inheritdoc />
    public Task<User?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    private Task<IReadOnlyList<byte[]>> EmailHashesAsync(string email, CancellationToken cancellationToken) =>
        blindIndexer.ComputeForLookupAsync(EncryptedFields.UserEmail, CryptoScopes.Platform, email, cancellationToken);

}