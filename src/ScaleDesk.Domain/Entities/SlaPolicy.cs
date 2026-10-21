using ScaleDesk.Domain.Common;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Domain.Entities;

/// <summary>
/// Tenant-defined response/resolution targets per priority. Entity only in this stage;
/// the SLA timers / breach engine land in a later stage (see spec §9).
/// </summary>
public class SlaPolicy : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = default!;

    public TicketPriority Priority { get; set; }

    public int ResponseDueInMinutes { get; set; }

    public int ResolutionDueInMinutes { get; set; }

    public bool IsDefault { get; set; }
}
