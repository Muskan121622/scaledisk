using ScaleDesk.Domain.Entities;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Application.Abstractions.Repositories;

public interface ICustomerRepository
{
    Task<Customer?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Customer?> FindByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Explicit tenant-scoped email lookup for the anonymous portal register/login flows, where
    /// no ambient tenant is bound and the permissive global filter would otherwise match any
    /// tenant's customer with the same email.
    /// </summary>
    Task<Customer?> FindByEmailInTenantAsync(Guid tenantId, string email, CancellationToken ct = default);
    Task AddAsync(Customer customer, CancellationToken ct = default);
    void Update(Customer customer);
    void Remove(Customer customer);
    Task<IReadOnlyList<Customer>> ListAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<long> CountAsync(string? search, CancellationToken ct = default);
}

public interface ITeamRepository
{
    Task<Team?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Team?> FindByNameAsync(string name, CancellationToken ct = default);
    Task AddAsync(Team team, CancellationToken ct = default);
    void Update(Team team);
    void Remove(Team team);
    Task<IReadOnlyList<Team>> ListAsync(CancellationToken ct = default);
}

public interface ITicketRepository
{
    Task<Ticket?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>customerId restricts the query to one customer's tickets — the portal's ownership filter.</summary>
    Task<IReadOnlyList<Ticket>> ListAsync(
        TicketStatus? status,
        TicketPriority? priority,
        Guid? assigneeUserId,
        int page,
        int pageSize,
        CancellationToken ct = default,
        Guid? customerId = null);

    Task<long> CountAsync(
        TicketStatus? status,
        TicketPriority? priority,
        Guid? assigneeUserId,
        CancellationToken ct = default,
        Guid? customerId = null);

    Task AddAsync(Ticket ticket, CancellationToken ct = default);
    void Update(Ticket ticket);
    void AddComment(TicketComment comment);
    void AddEvent(TicketEvent ticketEvent);

    /// <summary>Comments for a single ticket, oldest-first. Tenant-scoped by the global query filter.</summary>
    Task<IReadOnlyList<TicketComment>> GetCommentsAsync(Guid ticketId, CancellationToken ct = default);

    /// <summary>Append-only timeline entries for a single ticket, oldest-first. Tenant-scoped like comments.</summary>
    Task<IReadOnlyList<TicketEvent>> GetEventsAsync(Guid ticketId, CancellationToken ct = default);
}

public interface ISlaPolicyRepository
{
    Task<SlaPolicy?> GetAsync(Guid id, CancellationToken ct = default);
    Task<SlaPolicy?> FindByPriorityAsync(TicketPriority priority, CancellationToken ct = default);
    Task<SlaPolicy?> FindDefaultAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SlaPolicy>> ListAsync(CancellationToken ct = default);
    Task AddAsync(SlaPolicy policy, CancellationToken ct = default);
    void Update(SlaPolicy policy);
    void Remove(SlaPolicy policy);
}

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> ListForUserAsync(Guid recipientUserId, bool unreadOnly, int take, CancellationToken ct = default);
    Task<long> CountUnreadAsync(Guid recipientUserId, CancellationToken ct = default);
    Task<int> MarkReadAsync(Guid recipientUserId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<int> MarkAllReadAsync(Guid recipientUserId, CancellationToken ct = default);
}

public interface IAutomationRuleRepository
{
    Task<AutomationRule?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AutomationRule>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Enabled rules for a given tenant + trigger, ordered by priority. Filters by <paramref name="tenantId"/>
    /// explicitly (not the ambient tenant) because the automation engine runs off Kafka, where no request
    /// tenant is bound and the global filter is permissive (spec §10/§32).
    /// </summary>
    Task<IReadOnlyList<AutomationRule>> ListEnabledForEventAsync(Guid tenantId, string triggerEvent, CancellationToken ct = default);

    Task AddAsync(AutomationRule rule, CancellationToken ct = default);
    void Update(AutomationRule rule);
    void Remove(AutomationRule rule);
}
