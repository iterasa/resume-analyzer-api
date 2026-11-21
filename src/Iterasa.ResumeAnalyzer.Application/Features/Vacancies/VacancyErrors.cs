using Iterasa.ResumeAnalyzer.Domain.Vacancies;
using Iterasa.ResumeAnalyzer.Shared;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies;

/// <summary>Errores y conversiones comunes de los casos de uso de vacantes.</summary>
internal static class VacancyErrors {

    /// <summary>Mismo error exista en otra empresa o no exista: no se confirma qué ids existen.</summary>
    public static readonly Error NotFound = Error.NotFound("Vacancy not found.");

    public static readonly Error OwnerNotMember = Error.Validation("ownerId", "The owner must be an active member of the company.");

    public static IReadOnlyList<RequirementDraft> ToDrafts(IEnumerable<RequirementInput> inputs) =>
        [.. inputs.Select(input => new RequirementDraft(input.Id, input.Text, input.Kind, input.Weight))];

}