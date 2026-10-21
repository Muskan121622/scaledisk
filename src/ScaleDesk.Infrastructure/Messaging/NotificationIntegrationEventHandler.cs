using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Contracts.Notifications;
using ScaleDesk.Domain.Entities;
using ScaleDesk.Domain.Events;
using ScaleDesk.Infrastructure.Persistence;

namespace ScaleDesk.Infrastructure.Messaging;

/// <summary>
/// Projects integration events into user notifications (spec §22). It only stages rows and enqueues
/// real-time effects — dedup, the transaction and the commit are the orchestrator's job, so adding
/// another handler (automation, analytics) never has to repeat that machinery. New event types are
/// handled by adding a case here; unknown types fall through and are still recorded as processed
/// upstream, so an uninteresting event can never become a poison message that stalls the partition.
/// </summary>
public sealed class NotificationIntegrationEventHandler(
    ScaleDeskDbContext db,
    IClock clock,
    ILogger<NotificationIntegrationEventHandler> logger) : IIntegrationEventHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task HandleAsync(ConsumedIntegrationEvent message, IntegrationEventDispatch dispatch, CancellationToken ct = default)
    {
        switch (message.EventType)
        {
            case "ticket.assigned":
            {
                var evt = Deserialize<TicketAssigned>(message.PayloadJson);
                if (evt?.AssigneeUserId is { } assignee)
                {
                    var notification = Add(assignee, evt.TenantId, "ticket.assigned",
                        "A ticket was assigned to you", $"Ticket {evt.TicketId}", evt.TicketId);
                    EnqueuePush(dispatch, notification);
                }
                break;
            }
            case "sla.breached":
            {
                var evt = Deserialize<SlaBreached>(message.PayloadJson);
                if (evt is null) break;
                // The breach event carries no recipient, so resolve the ticket's current assignee.
                var assignee = await db.Tickets
                    .Where(t => t.Id == evt.TicketId)
                    .Select(t => t.AssigneeUserId)
                    .FirstOrDefaultAsync(ct);
                if (assignee is { } user)
                {
                    var notification = Add(user, evt.TenantId, "sla.breached",
                        $"SLA breached: {evt.BreachType}",
                        $"A ticket missed its {evt.BreachType.Replace('_', ' ')} deadline.", evt.TicketId);
                    EnqueuePush(dispatch, notification);
                }
                break;
            }
            default:
                // ticket.created / status_changed / resolved and non-ticket events produce no bell
                // entry; the board broadcast is handled once, centrally, by the orchestrator.
                logger.LogDebug("No notification projection for event type {EventType}.", message.EventType);
                break;
        }
    }

    private Notification Add(Guid recipient, Guid tenantId, string type, string title, string? body, Guid? ticketId)
    {
        var notification = new Notification
        {
            TenantId = tenantId,
            RecipientUserId = recipient,
            Type = type,
            Title = title,
            Body = body,
            RelatedTicketId = ticketId,
            IsRead = false,
            CreatedAt = clock.UtcNow
        };
        db.Notifications.Add(notification);
        return notification;
    }

    // Id + CreatedAt are already populated (client-generated Guid / set above), so the response can be
    // built now and pushed after commit without re-reading the tracked entity.
    private static void EnqueuePush(IntegrationEventDispatch dispatch, Notification n)
    {
        var payload = new NotificationResponse(
            n.Id, n.Type, n.Title, n.Body, n.RelatedTicketId, n.IsRead, n.CreatedAt);
        var recipient = n.RecipientUserId;
        dispatch.AfterCommit((realtime, ct) => realtime.NotificationCreatedAsync(recipient, payload, ct));
    }

    private static T? Deserialize<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
