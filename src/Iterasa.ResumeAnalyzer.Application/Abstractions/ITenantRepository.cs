using Iterasa.ResumeAnalyzer.Domain.Tenants;

namespace Iterasa.ResumeAnalyzer.Application.Abstractions;

/// <summary>Puerto de escritura de empresas y membresías.</summary>
internal interface ITenantRepository {

    /// <summary>
    /// Registra una empresa nueva junto con sus llaves de cifrado, en el mismo guardado: una
    /// empresa sin llaves no puede cifrar sus datos (ADR 0015).
    /// </summary>
    Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);

    void AddMembership(Membership membership);

    /// <summary>Empresa activa en la que el usuario entra por defecto: su membresía activa más antigua.</summary>
    Task<Guid?> FindDefaultTenantAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Indica si el usuario es miembro activo de la empresa, que también debe estar activa.</summary>
    Task<bool> IsActiveMemberAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

}