using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Domain.Constants;
using ScaleDesk.Domain.Entities;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Infrastructure.Persistence;

/// <summary>
/// Idempotent dev/demo seed so the API is immediately explorable after `migrate`. Runs with no
/// tenant bound (platform mode), where the query filter is intentionally permissive.
/// Each tenant is guarded separately so a system that was seeded before the demo workspace
/// existed still picks it up on next boot without disturbing existing rows.
/// </summary>
public sealed class DbSeeder(ScaleDeskDbContext db, IPasswordHasher hasher, ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (!await db.Tenants.AnyAsync(t => t.Slug == "technova", ct))
            SeedBusinessTenant("technova", "TechNova", "pro",
                ("rahul@technova.com", "Rahul", "Sharma"), ("sweta@technova.com", "Sweta", "Verma"),
                "Priya Nair", "priya@example.com", "Acme Retail",
                "Payment deducted but order missing", "My payment was deducted but my order wasn't created.",
                TicketCategory.Billing, TicketPriority.High);

        if (!await db.Tenants.AnyAsync(t => t.Slug == "acme", ct))
            SeedBusinessTenant("acme", "Acme Corp", "starter",
                ("dana@acme.com", "Dana", "Patel"), ("eli@acme.com", "Eli", "Khan"),
                "Marcus Lee", "marcus@widgetco.com", "WidgetCo",
                "Cannot export monthly report", "The CSV export button does nothing when clicked.",
                TicketCategory.Technical, TicketPriority.Medium);

        if (!await db.Tenants.AnyAsync(t => t.Slug == DemoWorkspace.Slug, ct))
            SeedDemoWorkspace();

        // ScaleDesk HQ account — global, member of no tenant. Kept outside the tenant seeds so an
        // existing dev database picks it up on next boot.
        if (!await db.Users.AnyAsync(u => u.Email == DemoWorkspace.PlatformEmail, ct))
        {
            db.Users.Add(new User
            {
                Email = DemoWorkspace.PlatformEmail, FirstName = "Platform", LastName = "Admin",
                PasswordHash = hasher.Hash(DemoWorkspace.OwnerPassword),
                EmailConfirmed = true, IsPlatformAdmin = true, CreatedAt = DateTime.UtcNow
            });
            logger.LogInformation("Seeded platform admin {Email} / {Pwd}.",
                DemoWorkspace.PlatformEmail, DemoWorkspace.OwnerPassword);
        }

        await db.SaveChangesAsync(ct);
    }

    private void SeedBusinessTenant(string slug, string name, string plan,
        (string Email, string First, string Last) ownerInfo, (string Email, string First, string Last) agentInfo,
        string customerName, string customerEmail, string customerCompany,
        string subject, string description, TicketCategory category, TicketPriority priority)
    {
        var now = DateTime.UtcNow;

        var tenant = new Tenant { Name = name, Slug = slug, Status = TenantStatus.Active, PlanTier = plan, CreatedAt = now };
        var owner = new User
        {
            Email = ownerInfo.Email, FirstName = ownerInfo.First, LastName = ownerInfo.Last,
            PasswordHash = hasher.Hash("Passw0rd!"), EmailConfirmed = true, CreatedAt = now
        };
        var agent = new User
        {
            Email = agentInfo.Email, FirstName = agentInfo.First, LastName = agentInfo.Last,
            PasswordHash = hasher.Hash("Passw0rd!"), EmailConfirmed = true, CreatedAt = now
        };
        var customer = new Customer
        {
            TenantId = tenant.Id, Name = customerName, Email = customerEmail,
            Company = customerCompany, Tags = new List<string> { "vip", "billing" }, CreatedAt = now
        };

        var defaultPolicy = new SlaPolicy { TenantId = tenant.Id, Name = "Default", Priority = TicketPriority.Medium, ResponseDueInMinutes = 240, ResolutionDueInMinutes = 2880, IsDefault = true, CreatedAt = now };
        var highPolicy = new SlaPolicy { TenantId = tenant.Id, Name = "High / Urgent", Priority = TicketPriority.High, ResponseDueInMinutes = 30, ResolutionDueInMinutes = 240, IsDefault = false, CreatedAt = now };

        var policy = priority == TicketPriority.High ? highPolicy : defaultPolicy;
        var ticket = new Ticket
        {
            TenantId = tenant.Id, CustomerId = customer.Id, Subject = subject, Description = description,
            Status = TicketStatus.Open, Priority = priority, Category = category, AssigneeUserId = agent.Id,
            SlaPolicyId = policy.Id,
            ResponseDueAt = now.AddMinutes(policy.ResponseDueInMinutes),
            ResolutionDueAt = now.AddMinutes(policy.ResolutionDueInMinutes),
            CreatedAt = now
        };

        db.Tenants.Add(tenant);
        db.Users.AddRange(owner, agent);
        db.Customers.Add(customer);
        db.SlaPolicies.AddRange(defaultPolicy, highPolicy);
        db.Memberships.AddRange(
            new Membership { TenantId = tenant.Id, UserId = owner.Id, Role = UserRole.Owner, CreatedAt = now },
            new Membership { TenantId = tenant.Id, UserId = agent.Id, Role = UserRole.Agent, CreatedAt = now });
        db.Tickets.Add(ticket);
        db.TicketEvents.Add(new TicketEvent
        {
            TenantId = tenant.Id, TicketId = ticket.Id, Type = "ticket.created",
            ActorUserId = owner.Id, Summary = "Seed ticket created", CreatedAt = now
        });

        logger.LogInformation("Seeded tenant '{Slug}' (owner {Email} / Passw0rd!).", slug, ownerInfo.Email);
    }

    /// <summary>
    /// The Stage 5F.1 "Try the demo" workspace. Owner login is
    /// <see cref="DemoWorkspace.OwnerEmail"/>, guarded by the same token pipeline as any other
    /// account. The data set is intentionally rich enough to look like a real support queue
    /// (multi-status tickets, comment threads, one visibly breached SLA row) but small enough to
    /// seed at startup without a separate script.
    /// </summary>
    private void SeedDemoWorkspace()
    {
        var now = DateTime.UtcNow;

        var tenant = new Tenant
        {
            Name = DemoWorkspace.Name, Slug = DemoWorkspace.Slug,
            Status = TenantStatus.Active, PlanTier = "demo", CreatedAt = now
        };
        var owner = new User
        {
            Email = DemoWorkspace.OwnerEmail, FirstName = "Sam", LastName = "Rivera",
            PasswordHash = hasher.Hash(DemoWorkspace.OwnerPassword),
            EmailConfirmed = true, CreatedAt = now
        };
        var agent = new User
        {
            Email = DemoWorkspace.AgentEmail, FirstName = "Ananya", LastName = "Iyer",
            PasswordHash = hasher.Hash(DemoWorkspace.OwnerPassword),
            EmailConfirmed = true, CreatedAt = now
        };

        db.Tenants.Add(tenant);
        db.Users.AddRange(owner, agent);
        db.Memberships.AddRange(
            new Membership { TenantId = tenant.Id, UserId = owner.Id, Role = UserRole.Owner, CreatedAt = now },
            new Membership { TenantId = tenant.Id, UserId = agent.Id, Role = UserRole.Agent, CreatedAt = now });

        var defaultPolicy = new SlaPolicy
        {
            TenantId = tenant.Id, Name = "Default (24h / 5d)", Priority = TicketPriority.Medium,
            ResponseDueInMinutes = 240, ResolutionDueInMinutes = 2880, IsDefault = true, CreatedAt = now
        };
        var highPolicy = new SlaPolicy
        {
            TenantId = tenant.Id, Name = "High (30m / 4h)", Priority = TicketPriority.High,
            ResponseDueInMinutes = 30, ResolutionDueInMinutes = 240, IsDefault = false, CreatedAt = now
        };
        var urgentPolicy = new SlaPolicy
        {
            TenantId = tenant.Id, Name = "Urgent (15m / 2h)", Priority = TicketPriority.Urgent,
            ResponseDueInMinutes = 15, ResolutionDueInMinutes = 120, IsDefault = false, CreatedAt = now
        };
        db.SlaPolicies.AddRange(defaultPolicy, highPolicy, urgentPolicy);

        // Curated cast for the demo workspace — small enough to seed at startup, rich enough to
        // fill every screen (list, detail, dashboard, sparkline, thread).
        var customers = new List<Customer>
        {
            new() { Name = "Priya Nair",     Email = "priya@acme-retail.example",     Company = "Acme Retail",   Tags = new List<string> { "vip", "billing" } },
            new() { Name = "Marcus Lee",     Email = "marcus@widgetco.example",       Company = "WidgetCo",      Tags = new List<string> { "trial" } },
            new() { Name = "Dana Patel",     Email = "dana@northwind.example",        Company = "Northwind",     Tags = new List<string>() },
            new() { Name = "Elena Kim",      Email = "elena@lumen.example",           Company = "Lumen Labs",    Tags = new List<string> { "enterprise" } },
            new() { Name = "Tobias Grant",   Email = "tobias@ferrousworks.example",   Company = "Ferrous",       Tags = new List<string> { "billing" } },
            new() { Name = "Amara Okafor",   Email = "amara@saharacloud.example",     Company = "Sahara Cloud",  Tags = new List<string> { "vip" } },
            new() { Name = "Ravi Menon",     Email = "ravi@indigo.example",           Company = "Indigo Pay",    Tags = new List<string>() },
            new() { Name = "Jules Arnaud",   Email = "jules@atelier.example",         Company = "Atelier",       Tags = new List<string> { "trial" } },
        };
        for (var i = 0; i < customers.Count; i++)
        {
            customers[i].TenantId = tenant.Id;
            customers[i].CreatedAt = now;
            db.Customers.Add(customers[i]);
        }

        var subjects = new (string Subject, string Description, TicketCategory Category, TicketPriority Priority, TicketStatus Status)[]
        {
            ("Payment deducted but order missing", "I paid twice for the annual plan this morning but the dashboard still shows my order as pending. Can you check?", TicketCategory.Billing,         TicketPriority.High,    TicketStatus.Open),
            ("Cannot export monthly report",       "The CSV export button on the Analytics page does nothing when clicked. Chrome 129.",                                                     TicketCategory.Bug,           TicketPriority.Medium,  TicketStatus.InProgress),
            ("Add SSO with Okta",                  "Our security team requires SSO before we can roll out to the whole org. Is Okta on the roadmap?",                                         TicketCategory.FeatureRequest,TicketPriority.Medium, TicketStatus.WaitingForCustomer),
            ("Two-factor codes rejected",          "TOTP codes from Authy stop working around the 20s mark. Time drift on our NTP?",                                                          TicketCategory.Technical,     TicketPriority.Urgent,  TicketStatus.Open),
            ("Refund request — duplicate charge",  "Was charged twice on Mar 3. Please refund the second charge.",                                                                            TicketCategory.Billing,         TicketPriority.High,    TicketStatus.Resolved),
            ("API returns 500 on bulk import",     "POST /v1/contacts with 2000+ items returns HTTP 500 after ~30s. Smaller batches are fine.",                                               TicketCategory.Technical,     TicketPriority.High,    TicketStatus.InProgress),
            ("Password reset email not delivered", "Never received the reset link. Checked spam. Tried three times.",                                                                         TicketCategory.Account,         TicketPriority.Medium,  TicketStatus.Closed),
            ("Wrong company name on invoice",      "Invoices show 'ATIELIER' instead of 'ATELIER'. Please fix for tax filing.",                                                              TicketCategory.Billing,         TicketPriority.Low,     TicketStatus.Open),
            ("Dark mode for embedded widget",      "Our app is dark by default; the embedded widget flashes white. Please add a theme prop.",                                                  TicketCategory.FeatureRequest,TicketPriority.Low,     TicketStatus.WaitingForInternal),
            ("Webhook deliveries delayed",         "Seeing 15–30 min latency on webhook deliveries since last night.",                                                                        TicketCategory.Technical,     TicketPriority.Urgent,  TicketStatus.InProgress),
            ("Seat count off by one",              "Billing shows 12 seats but we have 11 active users.",                                                                                     TicketCategory.Billing,         TicketPriority.Medium,  TicketStatus.Resolved),
            ("Data residency question",            "Where are customer backups stored? Compliance needs a written answer.",                                                                   TicketCategory.General,         TicketPriority.Low,     TicketStatus.Open),
        };

        for (var i = 0; i < subjects.Length; i++)
        {
            var s = subjects[i];
            var customer = customers[i % customers.Count];

            var policy = s.Priority switch
            {
                TicketPriority.High => highPolicy,
                TicketPriority.Urgent => urgentPolicy,
                _ => defaultPolicy
            };
            var createdAt = now.AddHours(-(i * 7 + 1)); // spread across the last ~4 days
            var responseDue = createdAt.AddMinutes(policy.ResponseDueInMinutes);
            var resolutionDue = createdAt.AddMinutes(policy.ResolutionDueInMinutes);
            Guid? assignee = i switch { var n when n % 3 == 0 => agent.Id, var n when n % 3 == 1 => owner.Id, _ => null };

            var ticket = new Ticket
            {
                TenantId = tenant.Id, CustomerId = customer.Id,
                Subject = s.Subject, Description = s.Description,
                Status = s.Status, Priority = s.Priority, Category = s.Category,
                AssigneeUserId = assignee,
                SlaPolicyId = policy.Id,
                ResponseDueAt = responseDue,
                ResolutionDueAt = resolutionDue,
                CreatedAt = createdAt
            };

            // Ticket #0 is intentionally shown as SLA-breached for the SLA timeline demo.
            if (i == 0)
            {
                ticket.SlaBreached = true;
                ticket.FirstResponseAt = responseDue.AddMinutes(45);
            }
            if (s.Status is TicketStatus.Resolved or TicketStatus.Closed)
            {
                ticket.ResolvedAt = createdAt.AddHours(2);
                ticket.FirstResponseAt ??= createdAt.AddMinutes(12);
            }

            db.Tickets.Add(ticket);

            // Thread for the first three tickets so the ticket-detail screen shows a real conversation.
            if (i < 3)
            {
                db.TicketComments.Add(new TicketComment
                {
                    TenantId = tenant.Id, TicketId = ticket.Id, CustomerId = customer.Id,
                    Body = s.Description, CreatedAt = createdAt
                });
                db.TicketComments.Add(new TicketComment
                {
                    TenantId = tenant.Id, TicketId = ticket.Id, AuthorUserId = agent.Id,
                    Body = "Thanks for flagging this — I'm looking into it now.",
                    CreatedAt = createdAt.AddMinutes(20)
                });
                db.TicketComments.Add(new TicketComment
                {
                    TenantId = tenant.Id, TicketId = ticket.Id, AuthorUserId = owner.Id,
                    Body = "Internal: escalated to the Billing team, ETA 1h.",
                    IsInternal = true, CreatedAt = createdAt.AddMinutes(35)
                });
            }

            db.TicketEvents.Add(new TicketEvent
            {
                TenantId = tenant.Id, TicketId = ticket.Id, Type = "ticket.created",
                ActorUserId = owner.Id, Summary = "Demo ticket seeded", CreatedAt = createdAt
            });
        }

        logger.LogInformation(
            "Seeded 'demo' workspace (owner {Email} / {Pwd}) for the Try-the-demo path.",
            DemoWorkspace.OwnerEmail, DemoWorkspace.OwnerPassword);
    }
}
