namespace ScaleDesk.Infrastructure.Caching;

/// <summary>
/// Redis connection settings bound from the "Redis" section. Disabled by default so local runs and
/// tests fall back to an in-memory distributed cache; enabling it is a single config flag.
/// </summary>
public sealed record RedisOptions
{
    public const string SectionName = "Redis";

    public bool Enabled { get; init; }

    public string ConnectionString { get; init; } = "localhost:6379";

    /// <summary>Key prefix so one Redis instance can host several environments safely.</summary>
    public string InstanceName { get; init; } = "scaledesk:";
}
