using Microsoft.Extensions.Caching.Distributed;
using System.Collections.Concurrent;
using System.Text.Json;

namespace DataImpl;

internal static class DistributedCacheExtensions
{
    internal static DistributedCacheEntryOptions DefaultExpiration => new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
    };

    private static readonly ConcurrentDictionary<string, Lazy<Task<object>>> _locks = [];

    /// <summary>
    /// Retrieves a cached value or creates and stores it in the cache if not found.
    /// </summary>
    /// <typeparam name="T">Type of the cached value</typeparam>
    /// <param name="cache"><see cref="IDistributedCache"/> instance</param>
    /// <param name="key">The cache key</param>
    /// <param name="factory">Function to generate the value if not found in cache</param>
    /// <param name="cacheEntryOptions">Optional cache entry expiration settings</param>
    /// <param name="serializerOptions">Optional JSON serializer settings</param>
    /// <returns>The cached or newly generated value</returns>
    /// <exception cref="InvalidOperationException"/>
    /// <exception cref="JsonException"/>
    /// <exception cref="NotSupportedException"/>
    internal static async Task<T> GetOrCreateAsync<T>(
        this IDistributedCache cache,
        string key,
        Func<Task<T>> factory,
        DistributedCacheEntryOptions? cacheEntryOptions = null,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default) where T : notnull
    {
        var cachedData = await cache.GetStringAsync(key, cancellationToken).ConfigureAwait(false);

        if (cachedData is not null)
        {
            return JsonSerializer.Deserialize<T>(cachedData, serializerOptions)!;
        }

        Lazy<Task<object>> lazyFactory = _locks.GetOrAdd(key, _ => new Lazy<Task<object>>(async () =>
        {
            T data = await factory().ConfigureAwait(false);

            await cache.SetStringAsync(
               key,
               JsonSerializer.Serialize(data, serializerOptions),
               cacheEntryOptions ?? DefaultExpiration).ConfigureAwait(false);

            return data;
        }));

        try
        {
            return (T)await lazyFactory.Value.ConfigureAwait(false);
        }
        finally
        {
            _locks.TryRemove(key, out _);
        }
    }
}
