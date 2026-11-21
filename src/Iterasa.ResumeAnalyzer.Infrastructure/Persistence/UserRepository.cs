using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Domain.Users;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using Microsoft.EntityFrameworkCore;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence;

/// <summary>
/// Lado de escritura del aggregate User.
/// </summary>
internal sealed class UserRepository(AppDbContext dbContext, BlindIndexer blindIndexer) : IUserRepository {

    /// <inheritdoc />
    public void Add(User user) => dbContext.Users.Add(user);

    /// <inheritdoc />
    /// <remarks>
    /// El correo está cifrado: se busca por su índice ciego, con cada versión vigente de la llave.
    /// </remarks>
    public async Task<bool> ExistsWithEmailAsync(string email, CancellationToken cancellationToken = default) {
        var hashes = await blindIndexer.ComputeForLookupAsync(EncryptedFields.UserEmail, CryptoScopes.Platform, email, cancellationToken);
        return await dbContext.Users.AnyAsync(
            u => hashes.Contains(EF.Property<byte[]>(u.Email, EncryptedFields.UserEmail.BlindIndexProperty)),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsWithUsernameAsync(string username, CancellationToken cancellationToken = default) {
        var normalized = username.Trim();
        return await dbContext.Users.AnyAsync(u => u.Username == normalized, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken = default) {
        return await dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

}