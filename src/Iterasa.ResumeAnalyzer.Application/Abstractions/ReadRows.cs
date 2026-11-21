using Iterasa.ResumeAnalyzer.Application.Features.Vacancies;
using Iterasa.ResumeAnalyzer.Shared.Tenancy;
using Iterasa.ResumeAnalyzer.Shared.Vacancies;

namespace Iterasa.ResumeAnalyzer.Application.Abstractions;

// Filas de lectura: lo que devuelven los puertos de consulta, antes de convertirse en lo que ve la web.

internal sealed record SessionUserRow(Guid Id, string Name, string Email, string? JobTitle, bool EmailVerified);

internal sealed record MembershipRow(Guid TenantId, string TenantName, MemberRole Role, bool AnonymousReview, DateTimeOffset CreatedAtUtc);

internal sealed record TenantSummaryRow(
    Guid Id,
    string Name,
    string Country,
    string PlanCode,
    PlanStatus PlanStatus,
    DateTimeOffset? TrialEndsAtUtc,
    int RetentionDays);

internal sealed record VacancyRow(
    Guid Id,
    string Title,
    string Area,
    Modality Modality,
    Priority Priority,
    VacancyStatus Status,
    CloseReason? CloseReason,
    DateOnly ClosesOn,
    DateOnly OpenedOn,
    DateOnly? ClosedOn,
    VacancyOwner Owner,
    string ApplyToken,
    IReadOnlyList<RequirementView> Requirements);