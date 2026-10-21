using Microsoft.EntityFrameworkCore;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Contracts.Dashboard;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Infrastructure.Persistence.Queries;

/// <summary>
/// Read-model for the Overview screen. Relies on the tenant query filter (and Postgres RLS beneath
/// it) for isolation — the same guards every other path uses, so a forgotten WHERE clause is
/// structurally impossible here. Kept deliberately simple: no new tables, no cache, no event hop.
/// </summary>
public sealed class EfDashboardQuery(ScaleDeskDbContext db, ICurrentUser user) : IDashboardQuery
{
    public async Task<DashboardStatsResponse> QueryAsync(int lookbackDays, CancellationToken ct = default)
    {
        // Clamp to a bounded window so the in-memory daily bucketing stays cheap even on a large tenant.
        var window = Math.Clamp(lookbackDays, 1, 90);
        var since = DateTime.UtcNow.Date.AddDays(-(window - 1));

        // Ticket counts by status — one round trip, one GROUP BY.
        var byStatus = await db.Tickets
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = (long)g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);

        long Get(TicketStatus s) => byStatus.TryGetValue(s, out var c) ? c : 0L;

        // SLA success rate: resolved tickets that closed before their resolutionDueAt, over resolved
        // tickets that had a deadline at all. Tickets without a policy are excluded (never scored).
        var scoredResolved = await db.Tickets
            .Where(t => t.ResolutionDueAt != null && (t.Status == TicketStatus.Resolved || t.Status == TicketStatus.Closed))
            .Select(t => new { Due = t.ResolutionDueAt!.Value, Closed = t.ResolvedAt ?? t.UpdatedAt ?? t.CreatedAt })
            .ToListAsync(ct);
        var slaSuccess = scoredResolved.Count == 0
            ? 1.0
            : (double)scoredResolved.Count(x => x.Closed <= x.Due) / scoredResolved.Count;

        // Daily volume for the sparkline: fetch bounded slice of CreatedAt timestamps, bucket in UTC.
        var recentDates = await db.Tickets
            .Where(t => t.CreatedAt >= since)
            .Select(t => t.CreatedAt)
            .ToListAsync(ct);
        var perDay = recentDates
            .GroupBy(d => DateOnly.FromDateTime(d))
            .ToDictionary(g => g.Key, g => (long)g.Count());
        var series = new List<DailyTicketCount>(window);
        for (var i = 0; i < window; i++)
        {
            var day = DateOnly.FromDateTime(since.AddDays(i));
            series.Add(new DailyTicketCount(day, perDay.TryGetValue(day, out var c) ? c : 0L));
        }

        var unread = user.UserId is { } uid
            ? await db.Notifications.LongCountAsync(n => n.RecipientUserId == uid && !n.IsRead, ct)
            : 0L;

        return new DashboardStatsResponse(
            Open: Get(TicketStatus.Open),
            InProgress: Get(TicketStatus.InProgress),
            Waiting: Get(TicketStatus.WaitingForCustomer) + Get(TicketStatus.WaitingForInternal),
            Resolved: Get(TicketStatus.Resolved),
            Closed: Get(TicketStatus.Closed),
            SlaSuccessRate: Math.Round(slaSuccess, 4),
            UnreadNotifications: unread,
            TicketsPerDay: series);
    }
}
