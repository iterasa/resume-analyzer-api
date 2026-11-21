using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Domain.Vacancies;
using Iterasa.ResumeAnalyzer.Shared;
using Iterasa.ResumeAnalyzer.Shared.Vacancies;

namespace Iterasa.ResumeAnalyzer.Application.Features.Vacancies;

/// <summary>
/// Lectura de vacantes ya convertidas en lo que ve la web: calcula los derivados (días abierta,
/// retención, enlaces) a partir de la fila y de la empresa.
/// </summary>
/// <remarks>
/// Los conteos de CV van a cero hasta que existan las postulaciones.
/// </remarks>
internal sealed class VacancyReader(
    ICurrentPrincipal principal,
    IVacancyQueries vacancies,
    ITenantQueries tenants,
    ICacheService cache,
    IPublicLinks links,
    TimeProvider clock) {

    private static readonly VacancySources NoSources = new(0, 0, 0);

    public async Task<PagedResult<VacancySummary>> ListAsync(VacancyStatus? status, PageQuery page, CancellationToken cancellationToken) {
        var rows = await vacancies.ListAsync(status, page, cancellationToken);
        var today = Today();
        return new PagedResult<VacancySummary>([.. rows.Items.Select(row => ToSummary(row, today))], rows.Page, rows.PageSize, rows.TotalCount);
    }

    public async Task<VacancyDetail?> FindAsync(Guid id, CancellationToken cancellationToken) {

        var row = await vacancies.FindAsync(id, cancellationToken);
        if (row is null) {
            return null;
        }

        var tenantId = principal.TenantId;
        var tenant = await cache.GetOrCreateAsync(
            CacheKeys.TenantSummary(tenantId),
            token => new ValueTask<TenantSummaryRow?>(tenants.FindSummaryAsync(tenantId, token)),
            CacheKeys.TenantSummaryPolicy,
            cancellationToken)
            ?? throw new InvalidOperationException($"Tenant {tenantId} of the current session was not found.");

        var summary = ToSummary(row, Today());

        return new VacancyDetail(
            summary.Id, summary.Title, summary.Area, summary.Modality, summary.Priority, summary.Status, summary.CloseReason,
            summary.ClosesOn, summary.Owner, summary.Received, summary.Analyzed, summary.OpenDays, summary.Sources,
            row.Requirements,
            Unreadable: 0,
            ActiveApplications: 0,
            tenant.RetentionDays,
            Vacancy.DeletesOn(row.ClosesOn, row.ClosedOn, tenant.RetentionDays),
            WhatsappLink: null,
            row.Status == VacancyStatus.Closed ? null : links.ApplyUrl(row.ApplyToken));

    }

    private static VacancySummary ToSummary(VacancyRow row, DateOnly today) => new(
        row.Id, row.Title, row.Area, row.Modality, row.Priority, row.Status, row.CloseReason, row.ClosesOn, row.Owner,
        Received: 0,
        Analyzed: 0,
        Vacancy.OpenDays(row.OpenedOn, row.ClosedOn, today),
        NoSources);

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

}