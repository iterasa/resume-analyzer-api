namespace Iterasa.ResumeAnalyzer.Domain.Common;

/// <summary>
/// Entidad que pertenece a una sola empresa. La persistencia filtra por la empresa de la
/// operación toda consulta sobre ellas y rechaza guardar una en otra empresa.
/// </summary>
internal interface ITenantOwned {

    /// <summary>Empresa dueña.</summary>
    Guid TenantId { get; }

}