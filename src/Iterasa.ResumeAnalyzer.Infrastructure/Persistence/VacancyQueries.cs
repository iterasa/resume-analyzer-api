using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Application.Features.Vacancies;
using Iterasa.ResumeAnalyzer.Shared;
using Iterasa.ResumeAnalyzer.Shared.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence;

/// <summary>Lado de lectura de vacantes de la empresa de la operación.</summary>
internal sealed class VacancyQueries(AppDbContext dbContext) : IVacancyQueries {

    /// <inheritdoc />
    public async Task<PagedResult<VacancyRow>> ListAsync(VacancyStatus? status, PageQuery page, CancellationToken cancellationToken = default) {

        var query = dbContext.Vacancies.AsNoTracking();

        if (status is { } wanted) {
            query = query.Where(vacancy => vacancy.Status == wanted);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(vacancy => vacancy.CreatedAtUtc)
            .ThenBy(vacancy => vacancy.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(vacancy => new {
                vacancy.Id,
                vacancy.Title,
                vacancy.Area,
                vacancy.Modality,
                vacancy.Priority,
                vacancy.Status,
                vacancy.CloseReason,
                vacancy.ClosesOn,
                vacancy.OpenedOn,
                vacancy.ClosedOn,
                vacancy.OwnerId,
                vacancy.ApplyToken,
            })
            .ToListAsync(cancellationToken);

        var owners = await OwnerNamesAsync([.. rows.Select(row => row.OwnerId)], cancellationToken);

        IReadOnlyList<VacancyRow> items = [.. rows.Select(row => new VacancyRow(
            row.Id, row.Title, row.Area, row.Modality, row.Priority, row.Status, row.CloseReason, row.ClosesOn, row.OpenedOn, row.ClosedOn,
            Owner(owners, row.OwnerId), row.ApplyToken, []))];

        return new PagedResult<VacancyRow>(items, page.Page, page.PageSize, totalCount);

    }

    /// <inheritdoc />
    public async Task<VacancyRow?> FindAsync(Guid id, CancellationToken cancellationToken = default) {

        var vacancy = await dbContext.Vacancies
            .AsNoTracking()
            .Include(candidate => candidate.Requirements)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (vacancy is null) {
            return null;
        }

        var owners = await OwnerNamesAsync([vacancy.OwnerId], cancellationToken);

        return new VacancyRow(
            vacancy.Id, vacancy.Title, vacancy.Area, vacancy.Modality, vacancy.Priority, vacancy.Status, vacancy.CloseReason,
            vacancy.ClosesOn, vacancy.OpenedOn, vacancy.ClosedOn, Owner(owners, vacancy.OwnerId), vacancy.ApplyToken,
            [.. vacancy.Requirements.OrderBy(requirement => requirement.Position)
                .Select(requirement => new RequirementView(requirement.Id, requirement.Text, requirement.Kind, requirement.Weight, Coverage: null))]);

    }

    /// <remarks>El nombre está cifrado: no se puede unir en la consulta, se lee aparte por id.</remarks>
    private async Task<Dictionary<Guid, string>> OwnerNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) {
        var distinct = ids.Distinct().ToList();
        return await dbContext.Users
            .AsNoTracking()
            .Where(user => distinct.Contains(user.Id))
            .Select(user => new { user.Id, user.Name })
            .ToDictionaryAsync(user => user.Id, user => user.Name, cancellationToken);
    }

    private static VacancyOwner Owner(Dictionary<Guid, string> owners, Guid id) =>
        new(id, owners.TryGetValue(id, out var name) ? name : string.Empty);

}