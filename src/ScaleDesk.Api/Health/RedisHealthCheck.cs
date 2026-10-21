using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace ScaleDesk.Api.Health;

/// <summary>
/// Readiness probe for the caching/queueing substrate (spec §41). Registered only when Redis is
/// enabled. Uses the shared multiplexer (created with AbortOnConnectFail=false), so a cold Redis
/// reports Degraded rather than failing the process at startup — the pod keeps serving from the
/// in-process cache path while the orchestrator watches readiness and holds traffic off a cold node.
/// </summary>
public sealed class RedisHealthCheck(IConnectionMultiplexer redis, ILogger<RedisHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!redis.IsConnected)
            return HealthCheckResult.Degraded("Redis connection is not established.");

        try
        {
            var latency = await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Redis reachable ({latency.TotalMilliseconds:F0}ms).");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis health probe failed.");
            return HealthCheckResult.Degraded(ex.Message, ex);
        }
    }
}
