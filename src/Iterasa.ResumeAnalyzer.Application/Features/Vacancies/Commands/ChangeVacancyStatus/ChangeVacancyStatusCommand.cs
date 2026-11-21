using Iterasa.ResumeAnalyzer.Shared.Vacancies;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.ChangeVacancyStatus;

/// <summary>Pausa, reanudación o reapertura de una vacante.</summary>
/// <param name="VacancyId">Vacante.</param>
/// <param name="Transition">Cambio de estado.</param>
/// <param name="ClosesOn">Al reabrir, fecha prevista de cierre nueva; obligatoria si la actual ya pasó.</param>
public sealed record ChangeVacancyStatusCommand(Guid VacancyId, VacancyTransition Transition, DateOnly? ClosesOn = null);