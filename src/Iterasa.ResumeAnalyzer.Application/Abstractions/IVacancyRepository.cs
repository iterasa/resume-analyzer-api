using Iterasa.ResumeAnalyzer.Domain.Vacancies;

namespace Iterasa.ResumeAnalyzer.Application.Abstractions;

/// <summary>Puerto de escritura del agregado Vacancy, en la empresa de la operación.</summary>
internal interface IVacancyRepository {

    void Add(Vacancy vacancy);

    /// <summary>Vacante con sus requisitos, rastreada. Nula si no existe o es de otra empresa.</summary>
    Task<Vacancy?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

}