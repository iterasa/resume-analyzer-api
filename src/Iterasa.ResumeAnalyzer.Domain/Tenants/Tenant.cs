using Iterasa.ResumeAnalyzer.Domain.Common;
using Iterasa.ResumeAnalyzer.Shared;
using Iterasa.ResumeAnalyzer.Shared.Tenancy;

namespace Iterasa.ResumeAnalyzer.Domain.Tenants;

/// <summary>
/// Empresa cliente: el límite de los datos. Todo lo de reclutamiento pertenece a una, y sus
/// datos personales se cifran con sus propias llaves (ADR 0015).
/// </summary>
internal sealed class Tenant : AuditableEntity {

    public const int MaxNameLength = 120;

    /// <summary>Días de retención por defecto de los CV tras el cierre (Privacidad).</summary>
    public const int DefaultRetentionDays = 90;

    /// <summary>Días de prueba de una empresa nueva. Pendiente de confirmar con el plan comercial.</summary>
    public const int TrialDays = 14;

    /// <summary>Código del plan de prueba.</summary>
    public const string TrialPlanCode = "trial";

    /// <summary>Países en los que opera Criba, tal como los muestra la web.</summary>
    public static readonly IReadOnlySet<string> SupportedCountries = new HashSet<string>(StringComparer.Ordinal) {
        "Guatemala", "El Salvador", "Honduras", "Costa Rica", "Panamá",
    };

    /// <summary>Retenciones que admite Privacidad.</summary>
    public static readonly IReadOnlySet<int> AllowedRetentionDays = new HashSet<int> { 90, 180, 365 };

    private Tenant() {
    }

    /// <summary>Identificador. Es también el ámbito de sus llaves de cifrado.</summary>
    public Guid Id { get; private set; }

    /// <summary>Nombre comercial.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>País, uno de <see cref="SupportedCountries" />.</summary>
    public string Country { get; private set; } = string.Empty;

    /// <summary>Plan contratado.</summary>
    public string PlanCode { get; private set; } = TrialPlanCode;

    /// <summary>Estado del plan.</summary>
    public PlanStatus PlanStatus { get; private set; }

    /// <summary>Fin de la prueba, si está en prueba.</summary>
    public DateTimeOffset? TrialEndsAtUtc { get; private set; }

    /// <summary>Días que se conservan los CV desde el cierre de la vacante.</summary>
    public int RetentionDays { get; private set; } = DefaultRetentionDays;

    /// <summary>Valor inicial de la evaluación anónima de los miembros nuevos.</summary>
    public bool AnonymousReviewByDefault { get; private set; } = true;

    /// <summary>
    /// Da de alta una empresa en prueba, con la retención por defecto y la evaluación anónima activada.
    /// </summary>
    /// <exception cref="DomainException">Nombre o país no válidos.</exception>
    public static Tenant Create(Guid id, string name, string country, Guid actorId, DateTimeOffset now) {

        if (id == Guid.Empty) {
            throw new DomainException("Id cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(country) || !SupportedCountries.Contains(country.Trim())) {
            throw new DomainException($"Country must be one of: {string.Join(", ", SupportedCountries)}.");
        }

        var tenant = new Tenant {
            Id = id,
            Name = DomainText.Required(name, "CompanyName", MaxNameLength),
            Country = country.Trim(),
            PlanCode = TrialPlanCode,
            PlanStatus = PlanStatus.Trial,
            TrialEndsAtUtc = now.AddDays(TrialDays),
        };

        tenant.SetCreated(actorId, now);
        return tenant;

    }

}