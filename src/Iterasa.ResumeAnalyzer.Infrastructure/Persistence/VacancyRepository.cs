using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence;

/// <summary>Lado de escritura de vacantes. El filtro de empresa del contexto acota cada consulta.</summary>
internal sealed class VacancyRepository(AppDbContext dbContext) : IVacancyRepository {

    /// <inheritdoc />
    public void Add(Vacancy vacancy) => dbContext.Vacancies.Add(vacancy);

    /// <inheritdoc />
    public Task<Vacancy?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Vacancies.Include(vacancy => vacancy.Requirements).FirstOrDefaultAsync(vacancy => vacancy.Id == id, cancellationToken);

}