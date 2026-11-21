using Iterasa.ResumeAnalyzer.Domain.Common;
using Iterasa.ResumeAnalyzer.Shared;
using Iterasa.ResumeAnalyzer.Shared.Vacancies;

namespace Iterasa.ResumeAnalyzer.Domain.Vacancies;

/// <summary>
/// Requisito de una vacante, contra el que se evalúa cada CV. Solo se modifica a través de
/// <see cref="Vacancy" />.
/// </summary>
internal sealed class Requirement : ITenantOwned {

    public const int MaxTextLength = 300;
    public const int MinWeight = 1;
    public const int MaxWeight = 5;

    private Requirement() {
    }

    /// <summary>Identificador. Los resultados del análisis lo referencian.</summary>
    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    /// <summary>Vacante dueña.</summary>
    public Guid VacancyId { get; private set; }

    /// <summary>Texto. Cambiarlo lo convierte en otro requisito a efectos del análisis.</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>Obligatorio o deseable.</summary>
    public RequirementKind Kind { get; private set; }

    /// <summary>Peso en el puntaje, de 1 a 5.</summary>
    public int Weight { get; private set; }

    /// <summary>Orden dentro de la vacante.</summary>
    public int Position { get; private set; }

    /// <summary>Peso que recibe un requisito nuevo si no se indica: 4 los obligatorios, 2 los deseables.</summary>
    public static int DefaultWeight(RequirementKind kind) => kind == RequirementKind.Mandatory ? 4 : 2;

    internal static Requirement Create(Guid id, Vacancy vacancy, string text, RequirementKind kind, int weight, int position) => new() {
        Id = id,
        TenantId = vacancy.TenantId,
        VacancyId = vacancy.Id,
        Text = NormalizeText(text),
        Kind = kind,
        Weight = CheckWeight(weight),
        Position = position,
    };

    /// <summary>Aplica un borrador. Devuelve si cambió el texto (hay que volver a evaluarlo).</summary>
    internal bool Apply(string text, RequirementKind kind, int weight, int position) {
        var normalized = NormalizeText(text);
        var textChanged = !string.Equals(Text, normalized, StringComparison.Ordinal);
        Text = normalized;
        Kind = kind;
        Weight = CheckWeight(weight);
        Position = position;
        return textChanged;
    }

    internal static string NormalizeText(string text) => DomainText.Required(text, "Requirement", MaxTextLength);

    private static int CheckWeight(int weight) =>
        weight is < MinWeight or > MaxWeight
            ? throw new DomainException($"Requirement weight must be between {MinWeight} and {MaxWeight}.")
            : weight;

}