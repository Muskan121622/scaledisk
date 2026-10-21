using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Abstractions.Repositories;
using ScaleDesk.Domain.Automation;
using ScaleDesk.Domain.Entities;
using ScaleDesk.Domain.Enums;
using ScaleDesk.Domain.Events;
using ScaleDesk.Infrastructure.Persistence;

namespace ScaleDesk.Infrastructure.Messaging;

/// <summary>
/// The workflow engine (spec §10). Reacts to "ticket.created" off the event stream by evaluating the
/// tenant's enabled automation rules and mutating the ticket accordingly (escalate priority, route to
/// a team/agent, flip status). Running here — not inline in ticket creation — is what keeps the write
/// path fast and lets a tenant add rules with zero deploy.
/// <para>
/// Crucially it changes the ticket via direct persistence, raising NO new domain events, so a rule
/// that matches its own output can never loop. Combined with the orchestrator's dedup + single
/// transaction, the same ticket.created being redelivered cannot apply a rule twice.
/// </para>
/// </summary>
public sealed class AutomationIntegrationEventHandler(
    ScaleDeskDbContext db,
    IAutomationRuleRepository rules,
    IClock clock,
    ILogger<AutomationIntegrationEventHandler> logger) : IIntegrationEventHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public const string Trigger = "ticket.created";

    public async Task HandleAsync(ConsumedIntegrationEvent message, IntegrationEventDispatch dispatch, CancellationToken ct = default)
    {
        if (message.EventType != Trigger)
            return;

        var evt = JsonSerializer.Deserialize<TicketCreated>(message.PayloadJson, Json);
        if (evt is null)
            return;

        var applicable = await rules.ListEnabledForEventAsync(evt.TenantId, Trigger, ct);
        if (applicable.Count == 0)
            return;

        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == evt.TicketId, ct);
        if (ticket is null)
            return;

        var changed = false;
        foreach (var rule in applicable)
        {
            if (!rule.Conditions.Matches(ticket.Priority, ticket.Category, ticket.Status, ticket.Subject))
                continue;

            foreach (var action in rule.Actions)
                changed |= Apply(ticket, action);

            logger.LogDebug("Automation rule {Rule} fired for ticket {Ticket} ({Event}).", rule.Id, ticket.Id, Trigger);
        }

        if (changed)
        {
            ticket.UpdatedAt = clock.UtcNow;
            db.Tickets.Update(ticket);
        }
    }

    private static bool Apply(Ticket ticket, AutomationAction action)
    {
        switch (action.Type)
        {
            case AutomationActionType.SetPriority when action.Priority is { } priority:
                if (ticket.Priority == priority) return false;
                ticket.Priority = priority;
                return true;
            case AutomationActionType.SetStatus when action.Status is { } status:
                if (ticket.Status == status) return false;
                ticket.Status = status;
                return true;
            case AutomationActionType.AssignTeam when action.TeamId is { } teamId:
                if (ticket.TeamId == teamId) return false;
                ticket.TeamId = teamId;
                return true;
            case AutomationActionType.AssignUser when action.AssigneeUserId is { } userId:
                if (ticket.AssigneeUserId == userId) return false;
                ticket.AssigneeUserId = userId;
                return true;
            default:
                return false;
        }
    }
}
