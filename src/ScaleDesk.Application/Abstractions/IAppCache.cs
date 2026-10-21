namespace ScaleDesk.Application.Abstractions;

/// <summary>
/// Application-level caching seam (spec §20). Services depend on this contract, never on Redis
/// directly, so the backing store (distributed cache vs. in-memory) is an Infrastructure decision.
/// Cache-aside is the intended pattern: read-through on miss, explicit invalidation on write.
/// A miss or a backend outage must degrade to "compute fresh", never to a failure.
/// </summary>
public interface IAppCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;

    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class;

    Task RemoveAsync(string key, CancellationToken ct = default);
}
