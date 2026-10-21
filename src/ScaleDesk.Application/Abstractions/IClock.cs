namespace ScaleDesk.Application.Abstractions;

/// <summary>
/// Time abstraction so services and tests stay deterministic instead of calling DateTime.UtcNow.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
