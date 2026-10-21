using ScaleDesk.Application.Abstractions;

namespace ScaleDesk.Infrastructure.Time;

/// <summary>Production clock. Always UTC to keep token expiry and SLA math timezone-agnostic.</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
