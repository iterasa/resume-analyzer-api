using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Application.Auditing;
using Iterasa.ResumeAnalyzer.Application.Common;
using Iterasa.ResumeAnalyzer.Application.Features.Me;
using Iterasa.ResumeAnalyzer.Domain.Tenants;
using Iterasa.ResumeAnalyzer.Domain.Users;
using Iterasa.ResumeAnalyzer.Domain.ValueObjects;
using Iterasa.ResumeAnalyzer.Shared;
using Iterasa.ResumeAnalyzer.Shared.Tenancy;
using Microsoft.Extensions.Logging;

namespace Iterasa.ResumeAnalyzer.Application.Features.Auth.Commands.Register;

/// <summary>
/// Caso de uso: registrar una empresa. Crea la empresa con sus llaves de cifrado, el usuario,
/// su membresía de administrador y la primera sesión, en un solo guardado.
/// </summary>
internal sealed class RegisterHandler(
    ICurrentPrincipal principal,
    IUserRepository users,
    ITenantRepository tenants,
    ITenantScope tenantScope,
    ISessionStore sessions,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    SessionComposer composer,
    TimeProvider clock,
    ILogger<RegisterHandler> logger) : IRegisterHandler {

    private const string Action = "tenant.register";

    /// <remarks>
    /// Confirma que el correo ya tiene cuenta: sin envío de correo no hay forma de responder
    /// igual exista o no. El limitador de la política <c>auth</c> acota la enumeración (ADR 0017).
    /// </remarks>
    private static readonly Error EmailTaken = Error.Conflict("An account with this email already exists.");

    /// <inheritdoc />
    public async Task<Result<SignedIn>> Handle(RegisterCommand command, CancellationToken cancellationToken = default) {

        if (!command.AcceptTerms) {
            return Result<SignedIn>.Failure(Error.Validation("acceptTerms", "The terms must be accepted."));
        }

        if (!PasswordPolicy.IsAcceptable(command.Password, out var violation)) {
            return Result<SignedIn>.Failure(Error.Validation("password", violation));
        }

        var now = clock.GetUtcNow();
        var userId = Guid.NewGuid();

        // El dominio valida antes de tocar la base o gastar en el hash.
        var tenant = Tenant.Create(Guid.NewGuid(), command.CompanyName, command.Country, userId, now);
        var email = Email.Create(command.Email);

        if (await users.ExistsWithEmailAsync(email.Value, cancellationToken)) {
            AuditLog.Rejected(logger, Guid.Empty, Action, "user:-", "email already registered");
            return Result<SignedIn>.Failure(EmailTaken);
        }

        var user = User.Create(new UserCreationArgs(userId, command.Name, email.Value, passwordHasher.Hash(command.Password), userId), now);

        await tenants.AddAsync(tenant, cancellationToken);
        users.Add(user);

        // Quien registra con otra sesión abierta no la deja viva detrás.
        if (principal.IsAuthenticated) {
            await sessions.RevokeAsync(principal.SessionId, now, cancellationToken);
        }

        IssuedSession credential;

        // La membresía es de la empresa nueva: se guarda dentro de su ámbito, como cualquier dato suyo.
        using (tenantScope.Enter(tenant.Id)) {
            tenants.AddMembership(Membership.Create(Guid.NewGuid(), tenant.Id, user.Id, MemberRole.Admin, tenant.AnonymousReviewByDefault, user.Id, now));
            credential = sessions.Open(user.Id, tenant.Id, persistent: false, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        AuditLog.Succeeded(logger, user.Id, Action, $"tenant:{tenant.Id}");

        var session = await composer.ComposeAsync(user.Id, tenant.Id, cancellationToken)
            ?? throw new InvalidOperationException("The session of a tenant just registered could not be composed.");

        return Result<SignedIn>.Success(new SignedIn(session, credential));

    }

}