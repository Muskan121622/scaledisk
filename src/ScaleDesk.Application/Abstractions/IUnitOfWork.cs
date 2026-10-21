namespace ScaleDesk.Application.Abstractions;

/// <summary>
/// Commits a unit of work. Repositories never save individually; a service mutates tracked
/// aggregates then calls SaveChangesAsync once, keeping related writes atomic.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
