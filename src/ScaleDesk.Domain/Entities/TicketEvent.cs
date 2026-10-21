using ScaleDesk.Domain.Common;

namespace ScaleDesk.Domain.Entities;

/// <summary>
/// Immutable, append-only entry on a ticket timeline (created / assigned / status changed /
/// resolved ...). Later feeds audit, analytics and event streaming (spec §8). Tenant-owned.
/// </summary>
public class TicketEvent : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    /// <summary>Stable machine name, e.g. "ticket.created", "ticket.assigned".</summary>
    public string Type { get; set; } = default!;

    public Guid? ActorUserId { get; set; }

    public string Summary { get; set; } = default!;

    /// <summary>JSON payload capturing before/after state (jsonb).</summary>
    public string? Payload { get; set; }
}
