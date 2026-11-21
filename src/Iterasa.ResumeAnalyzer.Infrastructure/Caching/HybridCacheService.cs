using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Microsoft.Extensions.Caching.Hybrid;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Caching;

/// <summary>
/// Adaptador de <see cref="ICacheService" /> sobre <see cref="HybridCache" /> (ADR 0019).
/// </summary>
/// <remarks>
/// Hoy solo hay memoria local (L1). Si se añade una caché distribuida (L2), las entradas con
/// datos personales siguen sin salir del proceso: se marcan para no escribirse en ella.
/// HybridCache además evita la estampida: una sola fábrica por clave a la vez.
/// </remarks>
internal sealed class HybridCacheService(HybridCache cache) : ICacheService {

    /// <inheritdoc />
    public ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, CachePolicy policy, CancellationToken cancellationToken = default) =>
        cache.GetOrCreateAsync(
            key,
            factory,
            static (state, token) => state(token),
            new HybridCacheEntryOptions {
                Expiration = policy.Expiration,
                LocalCacheExpiration = policy.Expiration,
                Flags = policy.ContainsPersonalData
                    ? HybridCacheEntryFlags.DisableDistributedCache
                    : HybridCacheEntryFlags.None,
            },
            cancellationToken: cancellationToken);

    /// <inheritdoc />
    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        cache.RemoveAsync(key, cancellationToken);

}