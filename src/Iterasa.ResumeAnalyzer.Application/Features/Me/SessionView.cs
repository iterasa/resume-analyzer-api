using Iterasa.ResumeAnalyzer.Shared.Tenancy;

namespace Iterasa.ResumeAnalyzer.Application.Features.Me;

/// <summary>Sesión actual: quién es, en qué empresa está y qué puede hacer.</summary>
/// <param name="User">Usuario.</param>
/// <param name="Tenant">Empresa activa.</param>
/// <param name="Memberships">Empresas a las que pertenece.</param>
/// <param name="Role">Rol en la empresa activa.</param>
/// <param name="Permissions">Permisos que concede ese rol.</param>
/// <param name="AnonymousReview">Evaluación anónima efectiva: los datos de los candidatos le llegan ocultos.</param>
public sealed record SessionView(
    SessionUser User,
    SessionTenant Tenant,
    IReadOnlyList<SessionMembership> Memberships,
    MemberRole Role,
    IReadOnlyList<string> Permissions,
    bool AnonymousReview);

/// <summary>Usuario de la sesión.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Name">Nombre.</param>
/// <param name="Email">Correo.</param>
/// <param name="JobTitle">Cargo.</param>
/// <param name="EmailVerified">Si verificó su correo.</param>
public sealed record SessionUser(Guid Id, string Name, string Email, string? JobTitle, bool EmailVerified);

/// <summary>Empresa activa.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Name">Nombre.</param>
/// <param name="Country">País.</param>
/// <param name="Plan">Plan.</param>
public sealed record SessionTenant(Guid Id, string Name, string Country, SessionPlan Plan);

/// <summary>Plan de la empresa.</summary>
/// <param name="Code">Código.</param>
/// <param name="Name">Nombre visible.</param>
/// <param name="Status">Estado.</param>
/// <param name="TrialEndsAt">Fin de la prueba, si está en prueba.</param>
public sealed record SessionPlan(string Code, string Name, PlanStatus Status, DateTimeOffset? TrialEndsAt);

/// <summary>Empresa a la que pertenece el usuario.</summary>
/// <param name="TenantId">Empresa.</param>
/// <param name="TenantName">Nombre.</param>
/// <param name="Role">Rol en ella.</param>
public sealed record SessionMembership(Guid TenantId, string TenantName, MemberRole Role);