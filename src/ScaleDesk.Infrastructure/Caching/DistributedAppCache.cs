using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using ScaleDesk.Application.Abstractions;

namespace ScaleDesk.Infrastructure.Caching;

/// <summary>
/// <see cref="IAppCache"/> over an <see cref="IDistributedCache"/>. The concrete distributed store
/// (Redis when enabled, in-memory otherwise) is chosen at registration, so this class is identical
/// whether or not a Redis server exists — keeps dev/test running while prod scales horizontally.
/// Serialization failures and cache misses degrade silently to a fresh compute.
/// </summary>
public sealed class DistributedAppCache(IDistributedCache cache) : IAppCache
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        try
        {
            var bytes = await cache.GetAsync(key, ct);
            return bytes is null ? null : JsonSerializer.Deserialize<T>(bytes, Options);
        }
        catch
        {
            // A cache must never bring down a read path; treat any failure as a miss.
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Options);
            await cache.SetAsync(key, bytes, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
        }
        catch
        {
            // Best-effort write; a failed cache-set just means the next read recomputes.
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await cache.RemoveAsync(key, ct);
        }
        catch
        {
            // Ignore: eviction is advisory.
        }
    }
}
