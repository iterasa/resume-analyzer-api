using Iterasa.ResumeAnalyzer.Domain.Common;
using Iterasa.ResumeAnalyzer.Shared;
using Iterasa.ResumeAnalyzer.Shared.Tenancy;

namespace Iterasa.ResumeAnalyzer.Domain.Tenants;

/// <summary>
/// Pertenencia de un usuario a una empresa, con su rol y su evaluación anónima.
/// </summary>
/// <remarks>
/// Raíz de su propio agregado: el equipo se gestiona membresía a membresía, sin cargar la
/// empresa ni el usuario. Invariante: un rol que no puede revelar datos evalúa siempre de
/// forma anónima.
/// </remarks>
internal sealed class Membership : AuditableEntity, ITenantOwned {

    private Membership() {
    }

    /// <summary>Identificador.</summary>
    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    /// <summary>Usuario.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Rol en la empresa.</summary>
    public MemberRole Role { get; private set; }

    /// <summary>
    /// Evaluación anónima: mientras esté activa, los datos personales de los candidatos le
    /// llegan ocultos. Siempre activa si el rol no concede <see cref="Permissions.CandidatesReveal" />.
    /// </summary>
    public bool AnonymousReview { get; private set; }

    /// <summary>Crea una membresía activa.</summary>
    /// <exception cref="DomainException">Identificadores vacíos.</exception>
    public static Membership Create(Guid id, Guid tenantId, Guid userId, MemberRole role, bool anonymousReview, Guid actorId, DateTimeOffset now) {

        if (id == Guid.Empty || tenantId == Guid.Empty || userId == Guid.Empty) {
            throw new DomainException("Membership ids cannot be empty.");
        }

        var membership = new Membership {
            Id = id,
            TenantId = tenantId,
            UserId = userId,
            Role = role,
            AnonymousReview = anonymousReview || !CanReveal(role),
        };

        membership.SetCreated(actorId, now);
        return membership;

    }

    /// <summary>Indica si el rol permite revelar datos personales.</summary>
    public static bool CanReveal(MemberRole role) => RolePermissions.Grants(role, Permissions.CandidatesReveal);

}