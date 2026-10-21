namespace ScaleDesk.Domain.Common;

/// <summary>
/// Base for all persisted aggregates. Identity is a client-generated <see cref="Guid"/>
/// so we never leak sequential / enumerable identifiers across tenants.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
