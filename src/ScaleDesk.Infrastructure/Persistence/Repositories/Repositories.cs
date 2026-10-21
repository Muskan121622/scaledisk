using Microsoft.EntityFrameworkCore;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Abstractions.Repositories;
using ScaleDesk.Domain.Entities;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(ScaleDeskDbContext db) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await db.Users.AddAsync(user, ct);
}

public sealed class TenantRepository(ScaleDeskDbContext db) : ITenantRepository
{
    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, ct);

    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task AddAsync(Tenant tenant, CancellationToken ct = default) =>
        await db.Tenants.AddAsync(tenant, ct);
}

public sealed class MembershipRepository(ScaleDeskDbContext db) : IMembershipRepository
{
    public Task<Membership?> GetAsync(Guid userId, Guid tenantId, CancellationToken ct = default) =>
        db.Memberships.IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.TenantId == tenantId, ct);

    public async Task<IReadOnlyList<Membership>> ListForUserAsync(Guid userId, CancellationToken ct = default) =>
        await db.Memberships.IgnoreQueryFilters()
            .Where(m => m.UserId == userId)
            .ToListAsync(ct);

    public async Task AddAsync(Membership membership, CancellationToken ct = default) =>
        await db.Memberships.AddAsync(membership, ct);

    public async Task SaveAsync(Membership membership, CancellationToken ct = default)
    {
        // New rows (default Guid would otherwise be tracked Added) vs. resurrecting a revoked row.
        if (db.Entry(membership).State == EntityState.Detached)
            await db.Memberships.AddAsync(membership, ct);
        else
            db.Memberships.Update(membership);
    }
}

public sealed class InvitationRepository(ScaleDeskDbContext db) : IInvitationRepository
{
    public Task<Invitation?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Invitations.FirstOrDefaultAsync(i => i.Id == id, ct);

    // The accept flow is anonymous (no tenant bound), so the token lookup must bypass the filter.
    // The token hash is unguessable, so the point read is still safe.
    public Task<Invitation?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.Invitations.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.TokenHash == tokenHash, ct);

    public Task<Invitation?> FindPendingByEmailAsync(string email, CancellationToken ct = default) =>
        db.Invitations.FirstOrDefaultAsync(i => i.Email == email && i.Status == InvitationStatus.Pending, ct);

    public async Task AddAsync(Invitation invitation, CancellationToken ct = default) =>
        await db.Invitations.AddAsync(invitation, ct);

    public void Update(Invitation invitation) => db.Invitations.Update(invitation);

    public async Task<IReadOnlyList<Invitation>> ListPendingAsync(CancellationToken ct = default) =>
        await db.Invitations
            .Where(i => i.Status == InvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);
}

public sealed class RefreshTokenRepository(ScaleDeskDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.RefreshTokens.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default) =>
        await db.RefreshTokens.AddAsync(token, ct);

    public void Update(RefreshToken token) => db.RefreshTokens.Update(token);
}

public sealed class CustomerUserRepository(ScaleDeskDbContext db) : ICustomerUserRepository
{
    // Portal register/login run anonymously (no ambient tenant), so both lookups take the tenant
    // id explicitly and bypass the filter — the same pattern membership login already uses.
    public Task<CustomerUser?> GetByUserIdAsync(Guid userId, Guid tenantId, CancellationToken ct = default) =>
        db.CustomerUsers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.UserId == userId && l.TenantId == tenantId, ct);

    public Task<CustomerUser?> GetByCustomerIdAsync(Guid customerId, Guid tenantId, CancellationToken ct = default) =>
        db.CustomerUsers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.CustomerId == customerId && l.TenantId == tenantId, ct);

    public async Task AddAsync(CustomerUser link, CancellationToken ct = default) =>
        await db.CustomerUsers.AddAsync(link, ct);
}

public sealed class CustomerRepository(ScaleDeskDbContext db) : ICustomerRepository
{
    public Task<Customer?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Customer?> FindByEmailAsync(string email, CancellationToken ct = default) =>
        db.Customers.FirstOrDefaultAsync(c => c.Email == email, ct);

    // Anonymous portal flows have no ambient tenant, so the tenant must be part of the predicate
    // or the permissive global filter would match the same email in ANY tenant.
    public Task<Customer?> FindByEmailInTenantAsync(Guid tenantId, string email, CancellationToken ct = default) =>
        db.Customers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Email == email, ct);

    public async Task AddAsync(Customer customer, CancellationToken ct = default) =>
        await db.Customers.AddAsync(customer, ct);

    public void Update(Customer customer) => db.Customers.Update(customer);

    public void Remove(Customer customer) => db.Customers.Remove(customer);

    public async Task<IReadOnlyList<Customer>> ListAsync(string? search, int page, int pageSize, CancellationToken ct = default) =>
        await ApplySearch(search)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public Task<long> CountAsync(string? search, CancellationToken ct = default) =>
        ApplySearch(search).LongCountAsync(ct);

    private IQueryable<Customer> ApplySearch(string? search)
    {
        // Global query filter already restricts rows to the current tenant.
        var query = db.Customers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c =>
                EF.Functions.ILike(c.Name, $"%{search}%") ||
                EF.Functions.ILike(c.Email, $"%{search}%") ||
                (c.Company != null && EF.Functions.ILike(c.Company, $"%{search}%")));
        return query;
    }
}

public sealed class TeamRepository(ScaleDeskDbContext db) : ITeamRepository
{
    public Task<Team?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Teams.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Team?> FindByNameAsync(string name, CancellationToken ct = default) =>
        db.Teams.FirstOrDefaultAsync(t => t.Name == name, ct);

    public async Task AddAsync(Team team, CancellationToken ct = default) =>
        await db.Teams.AddAsync(team, ct);

    public void Update(Team team) => db.Teams.Update(team);

    public void Remove(Team team) => db.Teams.Remove(team);

    public async Task<IReadOnlyList<Team>> ListAsync(CancellationToken ct = default) =>
        await db.Teams.OrderBy(t => t.Name).ToListAsync(ct);
}

public sealed class TicketRepository(ScaleDeskDbContext db, ICurrentTenant tenant) : ITicketRepository
{
    public Task<Ticket?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<Ticket>> ListAsync(
        TicketStatus? status, TicketPriority? priority, Guid? assigneeUserId,
        int page, int pageSize, CancellationToken ct = default, Guid? customerId = null)
    {
        return await ApplyFilters(status, priority, assigneeUserId, customerId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public Task<long> CountAsync(
        TicketStatus? status, TicketPriority? priority, Guid? assigneeUserId, CancellationToken ct = default, Guid? customerId = null) =>
        ApplyFilters(status, priority, assigneeUserId, customerId).LongCountAsync(ct);

    public async Task AddAsync(Ticket ticket, CancellationToken ct = default)
    {
        // Stamp the tenant server-side so a client can never target another tenant on write.
        ticket.TenantId = tenant.RequireTenant();
        await db.Tickets.AddAsync(ticket, ct);
    }

    public void AddComment(TicketComment comment) => db.TicketComments.Add(comment);

    public void AddEvent(TicketEvent ticketEvent) => db.TicketEvents.Add(ticketEvent);

    public async Task<IReadOnlyList<TicketComment>> GetCommentsAsync(Guid ticketId, CancellationToken ct = default) =>
        // Global tenant query filter + RLS already confine rows to the caller's workspace.
        await db.TicketComments
            .Where(c => c.TicketId == ticketId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TicketEvent>> GetEventsAsync(Guid ticketId, CancellationToken ct = default) =>
        // Same story as comments: the global filter + RLS keep this to the caller's tenant.
        await db.TicketEvents
            .Where(e => e.TicketId == ticketId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);

    public void Update(Ticket ticket) => db.Tickets.Update(ticket);

    private IQueryable<Ticket> ApplyFilters(TicketStatus? status, TicketPriority? priority, Guid? assigneeUserId, Guid? customerId)
    {
        // The global query filter already restricts to the current tenant.
        var query = db.Tickets.AsQueryable();
        if (status is not null) query = query.Where(t => t.Status == status);
        if (priority is not null) query = query.Where(t => t.Priority == priority);
        if (assigneeUserId is not null) query = query.Where(t => t.AssigneeUserId == assigneeUserId);
        if (customerId is not null) query = query.Where(t => t.CustomerId == customerId);
        return query;
    }
}

public sealed class SlaPolicyRepository(ScaleDeskDbContext db) : ISlaPolicyRepository
{
    public Task<SlaPolicy?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.SlaPolicies.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<SlaPolicy?> FindByPriorityAsync(TicketPriority priority, CancellationToken ct = default) =>
        db.SlaPolicies
            // An explicit per-priority policy wins over a catch-all default flagged for the same priority.
            .Where(s => s.Priority == priority)
            .OrderBy(s => s.IsDefault)
            .FirstOrDefaultAsync(ct);

    public Task<SlaPolicy?> FindDefaultAsync(CancellationToken ct = default) =>
        db.SlaPolicies.FirstOrDefaultAsync(s => s.IsDefault, ct);

    public async Task<IReadOnlyList<SlaPolicy>> ListAsync(CancellationToken ct = default) =>
        await db.SlaPolicies.OrderBy(s => s.Priority).ToListAsync(ct);

    public async Task AddAsync(SlaPolicy policy, CancellationToken ct = default) =>
        await db.SlaPolicies.AddAsync(policy, ct);

    public void Update(SlaPolicy policy) => db.SlaPolicies.Update(policy);

    public void Remove(SlaPolicy policy) => db.SlaPolicies.Remove(policy);
}

public sealed class NotificationRepository(ScaleDeskDbContext db) : INotificationRepository
{
    public async Task AddAsync(Notification notification, CancellationToken ct = default) =>
        await db.Notifications.AddAsync(notification, ct);

    public async Task<IReadOnlyList<Notification>> ListForUserAsync(Guid recipientUserId, bool unreadOnly, int take, CancellationToken ct = default)
    {
        var query = db.Notifications.Where(n => n.RecipientUserId == recipientUserId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);
        return await query.OrderByDescending(n => n.CreatedAt).Take(take).ToListAsync(ct);
    }

    public Task<long> CountUnreadAsync(Guid recipientUserId, CancellationToken ct = default) =>
        db.Notifications.LongCountAsync(n => n.RecipientUserId == recipientUserId && !n.IsRead, ct);

    // Set-based bulk update; filtered by recipient so a user can only ever mark their own rows.
    public async Task<int> MarkReadAsync(Guid recipientUserId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        await db.Notifications
            .Where(n => n.RecipientUserId == recipientUserId && ids.Contains(n.Id) && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);

    public async Task<int> MarkAllReadAsync(Guid recipientUserId, CancellationToken ct = default) =>
        await db.Notifications
            .Where(n => n.RecipientUserId == recipientUserId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
}

public sealed class AutomationRuleRepository(ScaleDeskDbContext db) : IAutomationRuleRepository
{
    // CRUD paths rely on the ambient tenant filter; the engine path passes an authoritative tenant id
    // and bypasses the filter so it is correct whether or not a request tenant is bound.
    public Task<AutomationRule?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.AutomationRules.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<AutomationRule>> ListAsync(CancellationToken ct = default) =>
        await db.AutomationRules.OrderBy(r => r.Priority).ThenBy(r => r.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<AutomationRule>> ListEnabledForEventAsync(Guid tenantId, string triggerEvent, CancellationToken ct = default) =>
        await db.AutomationRules
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId && r.IsEnabled && r.TriggerEvent == triggerEvent)
            .OrderBy(r => r.Priority)
            .ToListAsync(ct);

    public async Task AddAsync(AutomationRule rule, CancellationToken ct = default) =>
        await db.AutomationRules.AddAsync(rule, ct);

    public void Update(AutomationRule rule) => db.AutomationRules.Update(rule);

    public void Remove(AutomationRule rule) => db.AutomationRules.Remove(rule);
}

public sealed class UnitOfWork(ScaleDeskDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
