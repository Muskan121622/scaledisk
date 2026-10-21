using System.Diagnostics;
using Microsoft.Extensions.Primitives;
using Serilog.Context;
using ScaleDesk.Application.Constants;

namespace ScaleDesk.Api.Middleware;

/// <summary>
/// Outermost pipeline stage: establishes a correlation id for every request and emits one structured
/// completion log (method, path, status, latency, acting user + tenant). The id honours an inbound
/// X-Correlation-Id or a W3C traceparent, so it lines up with an upstream gateway / other services,
/// and is echoed back on the response so a support agent can quote it. Because this wraps the whole
/// pipeline via Serilog's LogContext, every log written further down (auth, services, the exception
/// handler, background work spawned in-request) carries the same CorrelationId — the thread that ties
/// a single trace across the distributed system together (spec §41).
/// </summary>
public sealed class RequestContextMiddleware(RequestDelegate next, ILogger<RequestContextMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        var start = Stopwatch.GetTimestamp();

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            try
            {
                await next(context);
            }
            finally
            {
                var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                var status = context.Response.StatusCode;
                var level = status switch { >= 500 => LogLevel.Error, >= 400 => LogLevel.Warning, _ => LogLevel.Information };
                var (user, tenant) = Actor(context);

                logger.Log(level,
                    "{Method} {Path} responded {Status} in {Elapsed:F0}ms (user={User}, tenant={Tenant})",
                    context.Request.Method, context.Request.Path, status, elapsedMs, user, tenant);
            }
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var incoming) && !StringValues.IsNullOrEmpty(incoming))
            return incoming.ToString();

        // Reuse the W3C trace-id so our id matches the ambient distributed trace when one exists.
        var traceparent = context.Request.Headers["traceparent"].ToString();
        if (ActivityContext.TryParse(traceparent, null, out var parent) && parent.TraceId != default)
            return parent.TraceId.ToHexString();

        var current = Activity.Current;
        if (current is not null && current.TraceId != default) return current.TraceId.ToHexString();

        return Guid.NewGuid().ToString("N");
    }

    private static (string? User, string? Tenant) Actor(HttpContext context)
    {
        var user = context.User?.FindFirst(AppClaims.Subject)?.Value;
        var tenant = context.User?.FindFirst(AppClaims.TenantId)?.Value;
        return (user, tenant);
    }
}
