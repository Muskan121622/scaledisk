using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Domain.Common;
using ScaleDesk.Domain.Entities;

namespace ScaleDesk.Infrastructure.Persistence;

/// <summary>
/// Single PostgreSQL-backed context. The heart of tenant isolation is the global query filter
/// applied to every <see cref="ITenantOwned"/> entity: it pins all reads to the tenant bound to
/// the current request, so a forgotten WHERE clause cannot leak another tenant's rows.
/// This is one of several layers (repository boundaries + authorization + later Postgres RLS).
/// <para>
/// It is also where the outbox is written: pending domain events collected during the request are
/// converted to <see cref="OutboxMessage"/> rows inside the very same <c>SaveChanges</c> transaction,
/// so a committed change and its event can never diverge (spec §31).
/// </para>
/// </summary>
public class ScaleDeskDbContext : DbContext
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly Guid? _currentTenantId;
    private readonly IEventCollector _events;

    public ScaleDeskDbContext(
        DbContextOptions<ScaleDeskDbContext> options,
        ICurrentTenant currentTenant,
        IEventCollector events)
        : base(options)
    {
        _currentTenantId = currentTenant.TenantId;
        _events = events;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerUser> CustomerUsers => Set<CustomerUser>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketEvent> TicketEvents => Set<TicketEvent>();
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ScaleDeskDbContext).Assembly);

        // Defense-in-depth: no tenant -> no rows for any tenant-owned entity.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(ScaleDeskDbContext)
                    .GetMethod(nameof(ApplyTenantFilter),
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(this, new object[] { modelBuilder });
            }
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => _currentTenantId == null || e.TenantId == _currentTenantId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AddOutboxMessages();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AddOutboxMessages();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Converts pending domain events into outbox rows so they commit atomically with the
    /// aggregate change. The collector is cleared afterwards to avoid double-writing on retries.
    /// </summary>
    private void AddOutboxMessages()
    {
        if (_events.Pending.Count == 0)
            return;

        foreach (var domainEvent in _events.Pending)
        {
            // Snapshot the current request's trace context so the async publish/consume can link back
            // to this exact HTTP call. Activity.Current is the ASP.NET Core request span when present.
            var traceParent = Activity.Current?.Id;
            var traceState = Activity.Current?.TraceStateString;

            OutboxMessages.Add(new OutboxMessage
            {
                TenantId = domainEvent.TenantId,
                EventType = domainEvent.EventType,
                Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), SerializerOptions),
                OccurredAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                TraceParent = traceParent,
                TraceState = traceState
            });
        }

        _events.Clear();
    }
}
