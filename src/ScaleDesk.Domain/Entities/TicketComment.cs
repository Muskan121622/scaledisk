using ScaleDesk.Domain.Common;

namespace ScaleDesk.Domain.Entities;

/// <summary>
/// A reply on a ticket's timeline. Tenant-owned. Internal notes are visible to staff only.
/// </summary>
public class TicketComment : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    /// <summary>Author may be a staff user or null for customer-originated replies.</summary>
    public Guid? AuthorUserId { get; set; }

    public Guid? CustomerId { get; set; }

    public string Body { get; set; } = default!;

    public bool IsInternal { get; set; }
}
