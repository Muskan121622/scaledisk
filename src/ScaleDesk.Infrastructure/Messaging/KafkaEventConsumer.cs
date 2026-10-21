using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Infrastructure.Diagnostics;

namespace ScaleDesk.Infrastructure.Messaging;

/// <summary>
/// Inbound half of the event pipeline (spec §32). Subscribes to every topic carrying the configured
/// prefix and hands each record to the registered <see cref="IIntegrationEventProcessor"/> inside its
/// own DI scope. Offsets are committed MANUALLY, only after the processor's transaction has landed —
/// so a crash before commit redelivers the record, and the processor's ProcessedMessage dedup makes
/// that safe. Net effect: at-least-once delivery + idempotent handling == exactly-once processing.
/// Disabled (a no-op) when Kafka:Enabled=false, matching the outbound processor.
/// </summary>
public sealed class KafkaEventConsumer(
    IServiceScopeFactory scopeFactory,
    KafkaOptions options,
    ILogger<KafkaEventConsumer> logger) : BackgroundService
{
    // The .NET (librdkafka) client has no regex subscribe, so we enumerate the integration-event
    // topics explicitly. This mirrors the IDomainEvent set in the Domain layer; add a topic here
    // when a new event type is published. Unhandled types still land in the processor's default
    // branch and are recorded-as-processed, so the list is a delivery allowlist, not a behaviour gate.
    private static readonly string[] SubscribedEventTypes =
    [
        "ticket.created", "ticket.assigned", "ticket.status_changed", "ticket.resolved", "sla.breached",
        "customer.created", "team.created", "member.invited", "user.email_verification_requested"
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Kafka event consumer disabled (Kafka:Enabled=false).");
            return;
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = options.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            // Commit only once the projection transaction succeeds (see ConsumeOneAsync).
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => logger.LogError("Kafka consumer error: {Error}", e.Reason))
            .Build();

        var topics = SubscribedEventTypes.Select(t => options.TopicPrefix + t);
        consumer.Subscribe(topics);
        logger.LogInformation("Kafka event consumer started (group={Group}, {Topics} topics).",
            options.ConsumerGroup, topics.Count());

        var poll = TimeSpan.FromMilliseconds(options.ConsumerPollTimeoutMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(poll);
                if (result is null || result.IsPartitionEOF)
                    continue;

                await ConsumeOneAsync(consumer, result, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never commit on failure; back off briefly and let redelivery retry the record.
                logger.LogError(ex, "Consume loop failed; retrying after backoff.");
                try { await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        consumer.Close();
        logger.LogInformation("Kafka event consumer stopped.");
    }

    private async Task ConsumeOneAsync(IConsumer<string, string> consumer, ConsumeResult<string, string> result, CancellationToken ct)
    {
        var eventType = result.Topic.Length > options.TopicPrefix.Length
            ? result.Topic[options.TopicPrefix.Length..]
            : result.Topic;

        var message = new ConsumedIntegrationEvent(
            result.Topic,
            result.Partition.Value,
            result.Offset.Value,
            eventType,
            result.Message.Value,
            EventId: GetValue(result.Message.Headers, "x-event-id"));

        // Continue the trace the producer put on the record: parse its W3C headers into a parent context
        // so this consumer activity (and everything under it — the projection, notification, automation)
        // shares the originating HTTP request's trace id rather than starting a fresh trace.
        var parent = ExtractContext(result.Message.Headers);
        using var activity = ScaledeskActivitySource.Source.StartActivity(
            "kafka consume", ActivityKind.Consumer, parent);
        if (activity is not null)
        {
            activity.SetTag(ScaledeskActivitySource.AttrSystem, ScaledeskActivitySource.SystemKafka);
            activity.SetTag(ScaledeskActivitySource.AttrDestination, result.Topic);
            activity.SetTag(ScaledeskActivitySource.AttrOperation, "process");
            activity.SetTag("messaging.event.type", eventType);
            activity.SetTag(ScaledeskActivitySource.AttrPartition, result.Partition.Value);
            activity.SetTag(ScaledeskActivitySource.AttrOffset, result.Offset.Value);
        }

        // A fresh scope per message: the processor is scoped (DbContext, unit of work).
        using var scope = scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IIntegrationEventProcessor>();

        await processor.ProcessAsync(message, ct);

        // Commit only after the projection + ProcessedMessage marker are durably stored.
        consumer.Commit(result);
    }

    private static ActivityContext ExtractContext(Headers? headers)
    {
        var traceParent = GetValue(headers, "traceparent");
        var traceState = GetValue(headers, "tracestate");
        return traceParent is not null && ActivityContext.TryParse(traceParent, traceState, out var ctx)
            ? ctx
            : default;
    }

    private static string? GetValue(Headers? headers, string key)
    {
        if (headers is null)
            return null;
        var header = headers.LastOrDefault(h => h.Key == key);
        var bytes = header?.GetValueBytes();
        return bytes is null or { Length: 0 } ? null : Encoding.UTF8.GetString(bytes);
    }
}
