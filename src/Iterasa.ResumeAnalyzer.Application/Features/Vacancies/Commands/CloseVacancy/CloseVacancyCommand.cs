using Iterasa.ResumeAnalyzer.Shared.Vacancies;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.CloseVacancy;

/// <summary>Cierre de una vacante.</summary>
/// <param name="VacancyId">Vacante.</param>
/// <param name="Reason">Motivo.</param>
/// <param name="HiredApplicationId">Postulación contratada, si se cubrió con una de la vacante.</param>
/// <param name="NotifyCandidates">Si se avisa a quienes siguen en proceso.</param>
public sealed record CloseVacancyCommand(Guid VacancyId, CloseReason Reason, Guid? HiredApplicationId, bool NotifyCandidates);