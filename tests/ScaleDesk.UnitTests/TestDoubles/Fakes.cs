using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Abstractions.Repositories;
using ScaleDesk.Domain.Common;
using ScaleDesk.Domain.Entities;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.UnitTests.TestDoubles;

// Minimal, hand-written doubles. The point of these tests is the guard logic (permission +
// tenant binding), which runs BEFORE any repository call, so the doubles mostly just need to
// exist and fail loudly if the service reaches them when it should not.

public sealed class FakeCurrentUser(Guid? userId, Guid? tenantId, IReadOnlySet<string> permissions, Guid? customerId = null) : ICurrentUser
{
    public Guid? UserId { get; } = userId;
    public Guid? TenantId { get; } = tenantId;
    public UserRole? Role { get; } = null;
    public Guid? CustomerId { get; } = customerId; // overrides the ICurrentUser DIM: set => portal session
    public IReadOnlySet<string> Permissions { get; } = permissions;
    public bool IsAuthenticated => UserId.HasValue;
    public bool HasPermission(string permission) => Permissions.Contains(permission);
}

public sealed class FakeCurrentTenant(Guid? tenantId) : ICurrentTenant
{
    public Guid? TenantId { get; } = tenantId;
    public bool IsAvailable => TenantId.HasValue;
    public Guid RequireTenant() =>
        TenantId ?? throw new ScaleDesk.Application.Exceptions.TenantNotResolvedException();
}

public sealed class FakeClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; } = utcNow;
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}

public sealed class FakeTicketRepository : ITicketRepository
{
    public Ticket? NextTicket { get; set; }
    public Ticket? AddedTicket { get; private set; }
    public TicketComment? AddedComment { get; private set; }
    public IReadOnlyList<TicketComment> Comments { get; set; } = Array.Empty<TicketComment>();

    public Task<Ticket?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(NextTicket);

    public Task<IReadOnlyList<Ticket>> ListAsync(TicketStatus? s, TicketPriority? p, Guid? a, int page, int size, CancellationToken ct = default, Guid? customerId = null)
        => Task.FromResult<IReadOnlyList<Ticket>>(Array.Empty<Ticket>());

    public Task<long> CountAsync(TicketStatus? s, TicketPriority? p, Guid? a, CancellationToken ct = default, Guid? customerId = null) => Task.FromResult(0L);

    public Task AddAsync(Ticket ticket, CancellationToken ct = default)
    {
        AddedTicket = ticket;
        return Task.CompletedTask;
    }

    public void Update(Ticket ticket) { }

    public void AddComment(TicketComment comment) => AddedComment = comment;
    public void AddEvent(TicketEvent ticketEvent) { }

    public Task<IReadOnlyList<TicketComment>> GetCommentsAsync(Guid ticketId, CancellationToken ct = default)
        => Task.FromResult(Comments);

    public Task<IReadOnlyList<TicketEvent>> GetEventsAsync(Guid ticketId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TicketEvent>>(Array.Empty<TicketEvent>());
}

public sealed class FakeSlaPolicyRepository : ISlaPolicyRepository
{
    public SlaPolicy? NextByPriority { get; set; }
    public SlaPolicy? NextDefault { get; set; }
    public IReadOnlyList<SlaPolicy> Policies { get; set; } = Array.Empty<SlaPolicy>();

    public Task<SlaPolicy?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult<SlaPolicy?>(null);
    public Task<SlaPolicy?> FindByPriorityAsync(TicketPriority priority, CancellationToken ct = default) => Task.FromResult(NextByPriority);
    public Task<SlaPolicy?> FindDefaultAsync(CancellationToken ct = default) => Task.FromResult(NextDefault);
    public Task<IReadOnlyList<SlaPolicy>> ListAsync(CancellationToken ct = default) => Task.FromResult(Policies);
    public Task AddAsync(SlaPolicy policy, CancellationToken ct = default) => Task.CompletedTask;
    public void Update(SlaPolicy policy) { }
    public void Remove(SlaPolicy policy) { }
}

public sealed class FakeCustomerRepository : ICustomerRepository
{
    public Customer? NextCustomer { get; set; }
    public Task<Customer?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(NextCustomer);
    public Task<Customer?> FindByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult<Customer?>(null);
    public Task<Customer?> FindByEmailInTenantAsync(Guid tenantId, string email, CancellationToken ct = default) => Task.FromResult<Customer?>(null);
    public Task AddAsync(Customer customer, CancellationToken ct = default) => Task.CompletedTask;
    public void Update(Customer customer) { }
    public void Remove(Customer customer) { }
    public Task<IReadOnlyList<Customer>> ListAsync(string? search, int page, int pageSize, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Customer>>(Array.Empty<Customer>());
    public Task<long> CountAsync(string? search, CancellationToken ct = default) => Task.FromResult(0L);
}

public sealed class FakeTeamRepository : ITeamRepository
{
    public Team? NextTeam { get; set; }
    public Task<Team?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(NextTeam);
    public Task<Team?> FindByNameAsync(string name, CancellationToken ct = default) => Task.FromResult<Team?>(null);
    public Task AddAsync(Team team, CancellationToken ct = default) => Task.CompletedTask;
    public void Update(Team team) { }
    public void Remove(Team team) { }
    public Task<IReadOnlyList<Team>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Team>>(Array.Empty<Team>());
}

public sealed class FakeAppCache : IAppCache
{
    private readonly Dictionary<string, object> _store = new();
    public int SetCount { get; private set; }
    public int RemoveCount { get; private set; }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class =>
        Task.FromResult(_store.TryGetValue(key, out var v) ? v as T : null);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class
    {
        _store[key] = value;
        SetCount++;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _store.Remove(key);
        RemoveCount++;
        return Task.CompletedTask;
    }
}

public sealed class FakeEventCollector : IEventCollector
{
    private readonly List<IDomainEvent> _pending = new();
    public IReadOnlyList<IDomainEvent> Raised => _pending;
    public void Raise(IDomainEvent domainEvent) => _pending.Add(domainEvent);
    public IReadOnlyList<IDomainEvent> Pending => _pending;
    public void Clear() => _pending.Clear();
}
