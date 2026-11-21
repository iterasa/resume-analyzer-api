using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Application.Auditing;
using Iterasa.ResumeAnalyzer.Application.Common;
using Iterasa.ResumeAnalyzer.Application.Features.Me;
using Iterasa.ResumeAnalyzer.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Iterasa.ResumeAnalyzer.Application.Features.Auth.Commands.SignIn;

/// <summary>
/// Caso de uso: iniciar sesión con correo y contraseña, con bloqueo por intentos fallidos. Abre
/// la sesión en la empresa más antigua del usuario.
/// </summary>
internal sealed class SignInHandler(
    ICurrentPrincipal principal,
    IUserRepository users,
    ITenantRepository tenants,
    ISessionStore sessions,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    SessionComposer composer,
    IOptions<SignInOptions> options,
    TimeProvider clock,
    ILogger<SignInHandler> logger) : ISignInHandler {

    private const string Action = "auth.sign-in";

    /// <inheritdoc />
    public async Task<Result<SignedIn>> Handle(SignInCommand command, CancellationToken cancellationToken = default) {

        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrEmpty(command.Password) || command.Password.Length > PasswordPolicy.MaximumLength) {
            AuditLog.Rejected(logger, Guid.Empty, Action, "user:-", "missing or oversized credentials");
            return Result<SignedIn>.Failure(AuthErrors.InvalidCredentials);
        }

        var now = clock.GetUtcNow();
        var user = await users.FindByEmailAsync(command.Email, cancellationToken);

        // Se verifica exista o no la cuenta: el tiempo de respuesta no debe delatarla.
        var passwordMatches = passwordHasher.Verify(command.Password, user?.PasswordHash);

        if (user is null) {
            AuditLog.Rejected(logger, Guid.Empty, Action, "user:-", "unknown email");
            return Result<SignedIn>.Failure(AuthErrors.InvalidCredentials);
        }

        if (!user.CanAuthenticate(now)) {
            AuditLog.Rejected(logger, user.Id, Action, $"user:{user.Id}", "user disabled, without password or locked out");
            return Result<SignedIn>.Failure(AuthErrors.InvalidCredentials);
        }

        if (!passwordMatches) {
            // Atómico: una ráfaga de intentos simultáneos cuenta entera para el bloqueo.
            var locked = await users.RecordFailedSignInAsync(user.Id, options.Value.MaxFailedAttempts, options.Value.LockoutDuration, now, cancellationToken);
            AuditLog.Rejected(logger, user.Id, Action, $"user:{user.Id}", locked ? "invalid password; locked out" : "invalid password");
            return Result<SignedIn>.Failure(AuthErrors.InvalidCredentials);
        }

        var tenantId = await tenants.FindDefaultTenantAsync(user.Id, cancellationToken);

        if (tenantId is null) {
            AuditLog.Rejected(logger, user.Id, Action, $"user:{user.Id}", "no active membership");
            return Result<SignedIn>.Failure(AuthErrors.InvalidCredentials);
        }

        // Único momento con la contraseña en claro: si el hash quedó atrás, se rehace aquí. Todo
        // en una actualización atómica, que no choca con intentos fallidos simultáneos (un 409
        // delataría que la contraseña de esa ráfaga era la buena).
        await users.RecordSuccessfulSignInAsync(
            user.Id,
            passwordHasher.NeedsRehash(user.PasswordHash!) ? passwordHasher.Hash(command.Password) : null,
            cancellationToken);

        // Quien inicia sesión con otra ya abierta no la deja viva detrás.
        if (principal.IsAuthenticated) {
            await sessions.RevokeAsync(principal.SessionId, now, cancellationToken);
        }

        var credential = sessions.Open(user.Id, tenantId.Value, command.RememberMe, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        AuditLog.Succeeded(logger, user.Id, Action, $"session:{credential.SessionId}");

        var session = await composer.ComposeAsync(user.Id, tenantId.Value, cancellationToken);

        return session is null
            ? Result<SignedIn>.Failure(AuthErrors.InvalidCredentials)
            : Result<SignedIn>.Success(new SignedIn(session, credential));

    }

}