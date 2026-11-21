using Iterasa.ResumeAnalyzer.Domain.Users;

namespace Iterasa.ResumeAnalyzer.Application.Abstractions;

/// <summary>
/// Puerto de escritura del agregado User. No persiste por su cuenta: el caso de uso
/// confirma con <see cref="IUnitOfWork.SaveChangesAsync" />.
/// </summary>
internal interface IUserRepository {

    void Add(User user);

    /// <summary>Busca por correo (índice ciego, con la normalización del correo).</summary>
    Task<bool> ExistsWithEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Usuario por correo, sin rastrear (solo para leer). Nulo si no existe.</summary>
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suma un intento fallido y, al llegar a <paramref name="maxAttempts" />, bloquea hasta
    /// <c>now + lockoutDuration</c> y reinicia la cuenta. Atómico en la base: los intentos
    /// simultáneos cuentan todos.
    /// </summary>
    /// <returns><see langword="true" /> si el usuario quedó bloqueado.</returns>
    Task<bool> RecordFailedSignInAsync(Guid userId, int maxAttempts, TimeSpan lockoutDuration, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inicio de sesión correcto: reinicia los fallos y, si se indica, sustituye el hash por otro de
    /// la misma contraseña con los parámetros vigentes. Atómico: no choca con intentos simultáneos.
    /// </summary>
    Task RecordSuccessfulSignInAsync(Guid userId, string? upgradedPasswordHash, CancellationToken cancellationToken = default);

    /// <summary>Usuario por id, rastreado para modificarlo. Nulo si no existe.</summary>
    Task<User?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

}