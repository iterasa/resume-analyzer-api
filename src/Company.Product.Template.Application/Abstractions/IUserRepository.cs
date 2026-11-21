using Company.Product.Template.Domain.Users;

namespace Company.Product.Template.Application.Abstractions;

/// <summary>
/// Puerto de escritura del aggregate User. No persiste por su cuenta: el caso de uso
/// confirma con <see cref="IUnitOfWork.SaveChangesAsync" />.
/// </summary>
internal interface IUserRepository {

    void Add(User user);

    Task<bool> ExistsWithEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> ExistsWithUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un usuario para modificarlo. Retorna null si no existe.
    /// </summary>
    Task<User?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

}