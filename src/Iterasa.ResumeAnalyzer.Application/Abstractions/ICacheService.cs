namespace Iterasa.ResumeAnalyzer.Application.Abstractions;

/// <summary>
/// Caché de lecturas frecuentes (ADR 0019). Aísla los casos de uso del proveedor: hoy memoria
/// local; mañana, si hace falta, una caché distribuida.
/// </summary>
/// <remarks>
/// Nunca se cachea lo que decide un acceso (sesiones, roles): eso se lee en cada petición. Toda
/// clave lleva la empresa (<see cref="CacheKeys" />).
/// </remarks>
internal interface ICacheService {

    /// <summary>Devuelve el valor cacheado o lo genera con la fábrica.</summary>
    ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, CachePolicy policy, CancellationToken cancellationToken = default);

    /// <summary>Invalida una entrada en esta instancia.</summary>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);

}

/// <summary>Vigencia y naturaleza de una entrada.</summary>
/// <param name="Expiration">Tiempo máximo que vale la entrada.</param>
/// <param name="ContainsPersonalData">
/// Lleva datos personales ya descifrados: nunca sale de la memoria del proceso, aunque se añada
/// una caché distribuida.
/// </param>
internal sealed record CachePolicy(TimeSpan Expiration, bool ContainsPersonalData);

/// <summary>Claves y políticas de la caché, en un solo sitio.</summary>
internal static class CacheKeys {

    /// <summary>Nombre, país y plan de una empresa. Sin datos personales.</summary>
    public static string TenantSummary(Guid tenantId) => $"tenant:{tenantId:N}:summary";

    /// <summary>Responsables posibles de una vacante: nombres del equipo.</summary>
    public static string OwnerOptions(Guid tenantId) => $"tenant:{tenantId:N}:owner-options";

    public static readonly CachePolicy TenantSummaryPolicy = new(TimeSpan.FromMinutes(5), ContainsPersonalData: false);

    public static readonly CachePolicy OwnerOptionsPolicy = new(TimeSpan.FromSeconds(60), ContainsPersonalData: true);

}