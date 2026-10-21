using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScaleDesk.Infrastructure.Persistence;

namespace ScaleDesk.Infrastructure.Messaging;

/// <summary>
/// Drains the outbox and publishes to Kafka. Runs on a timer in its own scope (no tenant bound, so
/// it sees every tenant's pending rows). Failures leave the row pending and record the error, so a
/// broker outage never loses an event — it just delays it (at-least-once, spec §31).
/// </summary>
public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    KafkaOptions options,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Outbox processor disabled (Kafka:Enabled=false); events remain buffered in the outbox.");
            return;
        }

        using var publisher = new KafkaEventPublisher(options, logger);
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(options.PollIntervalMilliseconds));

        logger.LogInformation("Outbox processor started (bootstrap={Bootstrap}).", options.BootstrapServers);

        do
        {
            try
            {
                await DrainAsync(publisher, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox drain cycle failed; will retry.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DrainAsync(KafkaEventPublisher publisher, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ScaleDeskDbContext>();

        var batch = await db.OutboxMessages
            .IgnoreQueryFilters()
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(options.BatchSize)
            .ToListAsync(ct);

        if (batch.Count == 0)
            return;

        foreach (var message in batch)
        {
            try
            {
                await publisher.PublishAsync(message.EventType, message.TenantId.ToString(), message.Payload,
                    message.TraceParent, message.TraceState, message.Id.ToString("D"), ct);
                message.ProcessedAt = DateTime.UtcNow;
                message.LastError = null;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.LastError = ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message;
                logger.LogWarning(ex, "Failed to publish outbox {Id} ({EventType}); attempt {Attempts}",
                    message.Id, message.EventType, message.Attempts);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
