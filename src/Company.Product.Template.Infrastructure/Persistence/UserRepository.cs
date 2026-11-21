using Company.Product.Template.Application.Abstractions;
using Company.Product.Template.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Company.Product.Template.Infrastructure.Persistence;

/// <summary>
/// Lado de escritura del aggregate User.
/// </summary>
internal sealed class UserRepository(AppDbContext dbContext) : IUserRepository {

    /// <inheritdoc />
    public void Add(User user) => dbContext.Users.Add(user);

    /// <inheritdoc />
    public async Task<bool> ExistsWithEmailAsync(string email, CancellationToken cancellationToken = default) {
        var normalized = email.Trim().ToLowerInvariant();
        return await dbContext.Users.AnyAsync(u => u.Email.Value == normalized, cancellationToken);
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