using ScaleDesk.Application.Contracts.Dashboard;

namespace ScaleDesk.Application.Abstractions;

/// <summary>
/// Read-only aggregation used by the dashboard screen. Lives in Application.Abstractions so the
/// Api depends on the abstraction and Infrastructure supplies the EF implementation, keeping the
/// Clean Architecture direction intact. Deliberately bypasses the write-side repositories because
/// this is a query, not a domain operation (CQRS-lite).
/// </summary>
public interface IDashboardQuery
{
    Task<DashboardStatsResponse> QueryAsync(int lookbackDays, CancellationToken ct = default);
}
