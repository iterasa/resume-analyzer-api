using Company.Product.Template.Domain.Common;
using Company.Product.Template.Domain.ValueObjects;
using Company.Product.Template.Shared;

namespace Company.Product.Template.Domain.Users;

/// <summary>
/// Usuario registrado en la API.
/// </summary>
/// <remarks>
/// Raíz del agregado. El límite es el usuario porque ese es el alcance de todas sus
/// operaciones: alta, baja y reactivación.
/// </remarks>
internal sealed class User : AuditableEntity {

    public const int MaxUsernameLength = 100;

    private User() {
    }

    /// <summary>Identificador.</summary>
    public Guid Id { get; private set; }

    /// <summary>Nombre único.</summary>
    public string Username { get; private set; } = string.Empty;

    /// <summary>Correo de contacto, único.</summary>
    public Email Email { get; private set; } = null!;

    /// <summary>
    /// Crea un usuario habilitado.
    /// </summary>
    /// <param name="args">Datos del alta.</param>
    /// <param name="now">Instante del alta.</param>
    /// <exception cref="DomainException">Cuando el identificador está vacío o el nombre o el correo no son válidos.</exception>
    public static User Create(UserCreationArgs args, DateTimeOffset now) {

        if (args.Id == Guid.Empty) {
            throw new DomainException("Id cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(args.Username)) {
            throw new DomainException("Username is required.");
        }

        if (args.Username.Trim().Length > MaxUsernameLength) {
            throw new DomainException($"Username cannot exceed {MaxUsernameLength} characters.");
        }

        // Caracteres de control (saltos de línea, ESC, NUL), de formato (anchura cero,
        // bidi) y separadores de línea: ninguno tiene sentido en un nombre, y viajan a los
        // logs y a PostgreSQL.
        if (UnsafeText.Contains(args.Username.Trim())) {
            throw new DomainException("Username cannot contain control, format or line separator characters.");
        }

        var user = new User {
            Id = args.Id,
            Username = args.Username.Trim(),
            Email = Email.Create(args.Email)
        };

        user.SetCreated(args.ActorId, now);
        return user;

    }

    // --- Estado ----------------------------------------------------------------------

    /// <summary>
    /// Da de baja al usuario.
    /// </summary>
    /// <remarks>
    /// Nadie puede darse de baja a sí mismo. Sin esta invariante, quien administra la API
    /// podía dejarse fuera.
    /// </remarks>
    public void Suspend(Guid actorId, DateTimeOffset now) {

        if (actorId == Id) {
            throw new DomainException("A user cannot disable itself.");
        }

        MarkDisabled(actorId, now);

    }

    /// <summary>Reactiva a un usuario dado de baja.</summary>
    public void Reinstate(Guid actorId, DateTimeOffset now) => MarkEnabled(actorId, now);

}