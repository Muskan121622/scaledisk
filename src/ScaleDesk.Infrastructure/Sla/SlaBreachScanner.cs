using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Domain.Enums;
using ScaleDesk.Domain.Events;
using ScaleDesk.Infrastructure.Persistence;

namespace ScaleDesk.Infrastructure.Sla;

/// <summary>
/// Standalone SLA enforcement loop (spec §9). Tickets that lapse past a deadline while nobody is
/// writing to them would otherwise never be flagged, so this scanner periodically marks overdue
/// tickets breached and emits <see cref="SlaBreached"/> events onto the outbox → Kafka.
/// <para>
/// It runs in its own DI scope with no HTTP context, so the tenant query filter is permissive and a
/// single pass can cover every tenant. Marking is idempotent (guarded by <c>SlaBreached</c>), so a
/// restart or a double tick never re-escalates the same ticket.
/// </para>
/// </summary>
public sealed class SlaBreachScanner(
    IServiceScopeFactory scopeFactory,
    SlaMonitorOptions options,
    ILogger<SlaBreachScanner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("SLA breach scanner disabled (Sla:Enabled=false).");
            return;
        }

        logger.LogInformation("SLA breach scanner started (interval {Interval}s).", options.IntervalSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.IntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SLA scan cycle failed; will retry next tick.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
                break;
        }
    }

    private async Task ScanOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ScaleDeskDbContext>();
        var events = scope.ServiceProvider.GetRequiredService<IEventCollector>();
        var now = DateTime.UtcNow;

        var breaches = await db.Tickets
            .Where(t => !t.SlaBreached &&
                        ((t.ResponseDueAt != null && t.FirstResponseAt == null && t.ResponseDueAt < now) ||
                         (t.ResolutionDueAt != null && t.ResolvedAt == null &&
                          t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed &&
                          t.ResolutionDueAt < now)))
            .OrderBy(t => t.CreatedAt)
            .Take(options.BatchSize)
            .ToListAsync(ct);

        foreach (var ticket in breaches)
        {
            // Resolution overdue takes precedence; otherwise the first-response clock was missed.
            var resolutionMissed = ticket.ResolutionDueAt is not null && ticket.ResolutionDueAt < now &&
                                   ticket.ResolvedAt is null;
            var breachType = resolutionMissed ? "resolution" : "first_response";
            var dueAt = resolutionMissed ? ticket.ResolutionDueAt!.Value : ticket.ResponseDueAt!.Value;

            ticket.SlaBreached = true;
            events.Raise(new SlaBreached(ticket.TenantId, ticket.Id, breachType, dueAt));
        }

        // Persists the flags AND the outbox rows in one transaction (SaveChanges drains the collector).
        await db.SaveChangesAsync(ct);

        if (breaches.Count > 0)
            logger.LogInformation("SLA scanner flagged {Count} breached ticket(s).", breaches.Count);
    }
}
