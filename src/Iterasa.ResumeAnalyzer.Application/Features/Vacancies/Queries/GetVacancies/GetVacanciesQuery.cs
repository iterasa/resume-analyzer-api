using Iterasa.ResumeAnalyzer.Shared;
using Iterasa.ResumeAnalyzer.Shared.Vacancies;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Queries.GetVacancies;

/// <summary>Listado de vacantes de la empresa activa.</summary>
/// <param name="Status">Solo las de este estado, o todas.</param>
/// <param name="Page">Página.</param>
public sealed record GetVacanciesQuery(VacancyStatus? Status, PageQuery Page);