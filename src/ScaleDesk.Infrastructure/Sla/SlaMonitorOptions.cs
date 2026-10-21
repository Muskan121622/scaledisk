namespace ScaleDesk.Infrastructure.Sla;

/// <summary>
/// Tuning for the SLA breach scanner, bound from the "Sla" config section. Disabled by default so
/// test/dev runs without a database or with an empty ticket backlog don't spin against nothing.
/// </summary>
public sealed record SlaMonitorOptions
{
    public const string SectionName = "Sla";

    public bool Enabled { get; init; }

    public int IntervalSeconds { get; init; } = 30;

    /// <summary>Max tickets escalated per cycle, so a big backlog drains gradually.</summary>
    public int BatchSize { get; init; } = 200;
}
