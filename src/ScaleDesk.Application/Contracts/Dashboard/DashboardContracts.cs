namespace ScaleDesk.Application.Contracts.Dashboard;

/// <summary>
/// Read-model for the workspace Overview screen (Stage 5F.1). Computed on demand from the
/// existing tickets/notifications tables — no new store, no cache, no Kafka hop.
/// </summary>
public sealed record DailyTicketCount(DateOnly Date, long Count);

public sealed record DashboardStatsResponse(
    long Open,
    long InProgress,
    long Waiting,
    long Resolved,
    long Closed,
    double SlaSuccessRate,
    long UnreadNotifications,
    IReadOnlyList<DailyTicketCount> TicketsPerDay);
