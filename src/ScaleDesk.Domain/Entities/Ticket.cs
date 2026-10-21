using ScaleDesk.Domain.Common;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Domain.Entities;

/// <summary>
/// Core domain aggregate. Tenant-owned. Carries a Postgres <c>xmin</c> row version so two
/// concurrent agents updating the same ticket cannot silently overwrite each other
/// (optimistic concurrency — see spec §30).
/// </summary>
public class Ticket : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string Subject { get; set; } = default!;

    public string Description { get; set; } = default!;

    public TicketStatus Status { get; set; } = TicketStatus.Open;

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    public TicketCategory Category { get; set; } = TicketCategory.General;

    public Guid? AssigneeUserId { get; set; }

    public Guid? TeamId { get; set; }
    public Team? Team { get; set; }

    public Guid? SlaPolicyId { get; set; }

    /// <summary>Wall-clock deadline to first response, resolved from the tenant's SLA policy on create.</summary>
    public DateTime? ResponseDueAt { get; set; }

    /// <summary>Wall-clock deadline to resolution.</summary>
    public DateTime? ResolutionDueAt { get; set; }

    /// <summary>Set once by the SLA breach scanner so an overdue ticket is only escalated once.</summary>
    public bool SlaBreached { get; set; }

    public DateTime? FirstResponseAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    /// <summary>Managed by Postgres (xmin). Concurrency token only — never assigned in code.</summary>
    public uint RowVersion { get; private set; }

    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();

    public ICollection<TicketEvent> Events { get; set; } = new List<TicketEvent>();
}
