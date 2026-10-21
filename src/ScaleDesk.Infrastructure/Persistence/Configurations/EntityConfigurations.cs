using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScaleDesk.Domain.Automation;
using ScaleDesk.Domain.Entities;

namespace ScaleDesk.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("tenants");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(80).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.PlanTier).HasMaxLength(40);
    }
}

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.Property(x => x.Email).HasMaxLength(320).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
    }
}

public sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> b)
    {
        b.ToTable("memberships");
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
        b.HasOne(x => x.Tenant).WithMany(t => t.Memberships).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany(u => u.Memberships).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> b)
    {
        b.ToTable("invitations");
        b.Property(x => x.Email).HasMaxLength(320).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        // Accept lookups are token-hash point reads; the pending-by-email guard is per tenant+email.
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.Email });
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.UserId });
    }
}

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("customers");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasMaxLength(320).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.Company).HasMaxLength(200);
        b.Property(x => x.Metadata).HasColumnType("jsonb");
        b.HasIndex(x => new { x.TenantId, x.Email });
    }
}

public sealed class CustomerUserConfiguration : IEntityTypeConfiguration<CustomerUser>
{
    public void Configure(EntityTypeBuilder<CustomerUser> b)
    {
        b.ToTable("customer_users");
        // One portal login represents at most one customer record per tenant; re-registering the
        // same email claims the same link instead of duplicating identities.
        b.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.CustomerId });
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> b)
    {
        b.ToTable("teams");
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
    }
}

public sealed class SlaPolicyConfiguration : IEntityTypeConfiguration<SlaPolicy>
{
    public void Configure(EntityTypeBuilder<SlaPolicy> b)
    {
        b.ToTable("sla_policies");
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.TenantId, x.Priority });
    }
}

public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> b)
    {
        b.ToTable("tickets");
        b.Property(x => x.Subject).HasMaxLength(300).IsRequired();
        b.Property(x => x.Description).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Category).HasConversion<string>().HasMaxLength(30);

        // Postgres system column xmin used as an optimistic concurrency token (spec §30).
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.Customer).WithMany(c => c.Tickets).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Team).WithMany(t => t.Tickets).HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.SetNull);

        // Hot query paths: a tenant's queue filtered by status / priority / assignee.
        b.HasIndex(x => new { x.TenantId, x.Status });
        b.HasIndex(x => new { x.TenantId, x.Priority });
        b.HasIndex(x => new { x.TenantId, x.AssigneeUserId });

        // The default ticket list is WHERE TenantId = @t ORDER BY CreatedAt DESC LIMIT/OFFSET (40% of
        // read traffic; Stage 5D found this as the bottleneck — an unindexed top-N sort over the whole
        // tenant partition, ~400ms / ~37k buffer reads per call). A composite index ordered (TenantId,
        // CreatedAt DESC) lets Postgres walk the index and stop at the page size — ~0.2ms, ~20 rows.
        b.HasIndex(x => new { x.TenantId, x.CreatedAt }).IsDescending(false, true);
    }
}

public sealed class TicketCommentConfiguration : IEntityTypeConfiguration<TicketComment>
{
    public void Configure(EntityTypeBuilder<TicketComment> b)
    {
        b.ToTable("ticket_comments");
        b.Property(x => x.Body).IsRequired();
        b.HasOne(x => x.Ticket).WithMany(t => t.Comments).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.TicketId });
    }
}

public sealed class TicketEventConfiguration : IEntityTypeConfiguration<TicketEvent>
{
    public void Configure(EntityTypeBuilder<TicketEvent> b)
    {
        b.ToTable("ticket_events");
        b.Property(x => x.Type).HasMaxLength(60).IsRequired();
        b.Property(x => x.Summary).HasMaxLength(500).IsRequired();
        b.Property(x => x.Payload).HasColumnType("jsonb");
        b.HasOne(x => x.Ticket).WithMany(t => t.Events).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.TicketId });
    }
}

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_messages");
        b.Property(x => x.EventType).HasMaxLength(80).IsRequired();
        b.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.LastError).HasMaxLength(2000);

        // The dispatcher polls pending rows oldest-first; a partial index on unprocessed keeps it cheap.
        b.HasIndex(x => x.ProcessedAt);
        b.HasIndex(x => new { x.ProcessedAt, x.OccurredAt });
    }
}

public sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> b)
    {
        b.ToTable("processed_messages");
        b.Property(x => x.DeduplicationKey).HasMaxLength(300).IsRequired();
        // The uniqueness of (topic, partition, offset) is exactly the idempotency guarantee.
        b.HasIndex(x => x.DeduplicationKey).IsUnique();
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        b.Property(x => x.Type).HasMaxLength(60).IsRequired();
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        // Bell query: a recipient's unread notifications, newest first.
        b.HasIndex(x => new { x.TenantId, x.RecipientUserId, x.IsRead });
        b.HasIndex(x => x.CreatedAt);
    }
}

public sealed class AutomationRuleConfiguration : IEntityTypeConfiguration<AutomationRule>
{
    // Enums are stored by name so the jsonb stays human-auditable and stable across renumbers.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public void Configure(EntityTypeBuilder<AutomationRule> b)
    {
        b.ToTable("automation_rules");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.TriggerEvent).HasMaxLength(80).IsRequired();

        // Conditions + Actions are structured documents; jsonb keeps the schema stable while the
        // rule vocabulary grows, without a column per field or a bespoke parser.
        // Each POCO is deserialised to a fresh instance on load, so EF needs a value comparer to
        // detect real changes instead of flagging the column dirty on every SaveChanges. Both types
        // are records (structural equality), so SequenceEqual / Equals give an exact comparison.
        b.Property(x => x.Conditions)
            .HasConversion(
                v => JsonSerializer.Serialize(v, Json),
                v => JsonSerializer.Deserialize<AutomationConditions>(v, Json) ?? new())
            .HasColumnType("jsonb")
            .Metadata.SetValueComparer(new ValueComparer<AutomationConditions>(
                (l, r) => l == null ? r == null : l.Equals(r),
                v => v == null ? 0 : v.GetHashCode(),
                v => v));

        b.Property(x => x.Actions)
            .HasConversion(
                v => JsonSerializer.Serialize(v, Json),
                v => JsonSerializer.Deserialize<List<AutomationAction>>(v, Json) ?? new())
            .HasColumnType("jsonb")
            .Metadata.SetValueComparer(new ValueComparer<List<AutomationAction>>(
                (l, r) => l != null && r != null && l.SequenceEqual(r),
                v => v == null ? 0 : v.Aggregate(0, (acc, x) => HashCode.Combine(acc, x)),
                v => v == null ? null! : v.ToList()));

        // The engine's hot lookup: enabled rules for a tenant keyed by trigger event, ordered by priority.
        b.HasIndex(x => new { x.TenantId, x.TriggerEvent, x.IsEnabled });
    }
}
