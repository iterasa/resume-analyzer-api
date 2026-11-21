namespace Iterasa.ResumeAnalyzer.Application.Abstractions;

/// <summary>Puerto de lectura de empresas.</summary>
internal interface ITenantQueries {

    Task<TenantSummaryRow?> FindSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default);

}