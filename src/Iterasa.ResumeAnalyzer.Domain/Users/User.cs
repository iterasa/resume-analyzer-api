using Iterasa.ResumeAnalyzer.Domain.Common;
using Iterasa.ResumeAnalyzer.Domain.ValueObjects;
using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Domain.Users;

/// <summary>
/// Persona del equipo de una o varias empresas: quien inicia sesión.
/// </summary>
/// <remarks>
/// Raíz del agregado. No pertenece a una empresa: su papel en cada una lo dice su membresía
/// (<see cref="Tenants.Membership" />). Por eso sus datos van con las llaves de plataforma
/// (ADR 0015, decisión 9).
/// </remarks>
internal sealed class User : AuditableEntity {

    public const int MaxNameLength = 100;
    public const int MaxJobTitleLength = 100;
    public const int MaxPasswordHashLength = 512;

    private User() {
    }

    /// <summary>Identificador. Es el claim <c>sub</c> de su sesión.</summary>
    public Guid Id { get; private set; }

    /// <summary>Nombre visible. Cifrado.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Correo con el que inicia sesión, único. Cifrado y buscable por índice ciego.</summary>
    public Email Email { get; private set; } = null!;

    /// <summary>Cargo, opcional.</summary>
    public string? JobTitle { get; private set; }

    /// <summary>Hash de la contraseña, con su algoritmo y parámetros. Nulo si aún no la tiene.</summary>
    public string? PasswordHash { get; private set; }

    /// <summary>Intentos de inicio de sesión fallidos consecutivos. Lo actualiza el repositorio de forma atómica.</summary>
    public int FailedSignInAttempts { get; private set; }

    /// <summary>Instante hasta el que está bloqueado por intentos fallidos.</summary>
    public DateTimeOffset? LockedUntilUtc { get; private set; }

    /// <summary>Instante en que verificó su correo. Nulo mientras no lo haga.</summary>
    public DateTimeOffset? EmailVerifiedAtUtc { get; private set; }

    /// <summary>Indica si verificó su correo.</summary>
    public bool IsEmailVerified => EmailVerifiedAtUtc is not null;

    /// <summary>
    /// Crea un usuario habilitado.
    /// </summary>
    /// <exception cref="DomainException">Cuando el identificador está vacío o el nombre, el correo o el hash no son válidos.</exception>
    public static User Create(UserCreationArgs args, DateTimeOffset now) {

        if (args.Id == Guid.Empty) {
            throw new DomainException("Id cannot be empty.");
        }

        var user = new User {
            Id = args.Id,
            Name = DomainText.Required(args.Name, "Name", MaxNameLength),
            Email = Email.Create(args.Email),
            PasswordHash = args.PasswordHash is null ? null : RequireHash(args.PasswordHash),
        };

        user.SetCreated(args.ActorId, now);
        return user;

    }

    // --- Autenticación -------------------------------------------------------------

    /// <summary>Indica si está bloqueado por intentos fallidos en el instante indicado.</summary>
    public bool IsLockedOut(DateTimeOffset now) => LockedUntilUtc is not null && LockedUntilUtc > now;

    /// <summary>Indica si puede iniciar sesión en el instante indicado.</summary>
    public bool CanAuthenticate(DateTimeOffset now) => IsEnabled && PasswordHash is not null && !IsLockedOut(now);

    // Los intentos fallidos, el bloqueo y el rehash no tienen verbo aquí: los escribe el
    // repositorio con una actualización atómica. Leer, sumar y guardar perdería los intentos
    // simultáneos y dejaría probar contraseñas en ráfagas sin llegar al bloqueo.

    // --- Estado ----------------------------------------------------------------------

    /// <summary>
    /// Da de baja al usuario. Nadie puede darse de baja a sí mismo.
    /// </summary>
    public void Suspend(Guid actorId, DateTimeOffset now) {

        if (actorId == Id) {
            throw new DomainException("A user cannot disable itself.");
        }

        MarkDisabled(actorId, now);

    }

    /// <summary>Reactiva a un usuario dado de baja.</summary>
    public void Reinstate(Guid actorId, DateTimeOffset now) => MarkEnabled(actorId, now);

    private static string RequireHash(string passwordHash) =>
        string.IsNullOrWhiteSpace(passwordHash) || passwordHash.Length > MaxPasswordHashLength
            ? throw new DomainException("PasswordHash is invalid.")
            : passwordHash;

}