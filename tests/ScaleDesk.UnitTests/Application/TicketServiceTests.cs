using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Constants;
using ScaleDesk.Application.Contracts.Tickets;
using ScaleDesk.Application.Exceptions;
using ScaleDesk.Application.Services;
using ScaleDesk.Domain.Constants;
using ScaleDesk.Domain.Entities;
using ScaleDesk.Domain.Enums;
using ScaleDesk.UnitTests.TestDoubles;

namespace ScaleDesk.UnitTests.Application;

/// <summary>
/// Verifies that the ticket service enforces authorization and tenant binding at the use-case
/// boundary — the layer that protects against cross-tenant access regardless of the HTTP edge.
/// </summary>
public class TicketServiceTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly DateTime FixedNow = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    private static TicketService Build(FakeTicketRepository tickets, ICurrentUser user, ICurrentTenant tenant)
    {
        var sla = new FakeSlaPolicyRepository();
        return new TicketService(tickets, new FakeCustomerRepository(), sla, tenant, user,
            new FakeUnitOfWork(), new FakeClock(FixedNow), new FakeEventCollector(), new FakeAppCache());
    }

    private static CreateTicketRequest ValidRequest() =>
        new(Guid.NewGuid(), "Payment missing", "Charged but no order", TicketPriority.High, TicketCategory.Billing, null, null);

    [Fact]
    public async Task Create_without_permission_is_forbidden()
    {
        var user = new FakeCurrentUser(Guid.NewGuid(), TenantA, new HashSet<string> { Permissions.TicketsRead });
        var svc = Build(new FakeTicketRepository(), user, new FakeCurrentTenant(TenantA));

        await Assert.ThrowsAsync<ForbiddenException>(() => svc.CreateAsync(ValidRequest()));
    }

    [Fact]
    public async Task Create_without_bound_tenant_is_rejected()
    {
        var user = new FakeCurrentUser(Guid.NewGuid(), null, new HashSet<string> { Permissions.TicketsCreate });
        var svc = Build(new FakeTicketRepository(), user, new FakeCurrentTenant(null)); // no tenant

        await Assert.ThrowsAsync<TenantNotResolvedException>(() => svc.CreateAsync(ValidRequest()));
    }

    [Fact]
    public async Task Get_missing_ticket_is_not_found_not_cross_tenant_leak()
    {
        // Repository (tenant-filtered) returns null for a foreign id -> surfaces as 404, never another tenant's data.
        var user = new FakeCurrentUser(Guid.NewGuid(), TenantA, new HashSet<string> { Permissions.TicketsRead });
        var svc = Build(new FakeTicketRepository { NextTicket = null }, user, new FakeCurrentTenant(TenantA));

        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Update_to_resolved_stamps_resolved_time()
    {
        var ticket = new Ticket { TenantId = TenantA, CustomerId = Guid.NewGuid(), Subject = "x", Description = "y", Status = TicketStatus.InProgress };
        var user = new FakeCurrentUser(Guid.NewGuid(), TenantA, new HashSet<string> { Permissions.TicketsUpdate });
        var svc = Build(new FakeTicketRepository { NextTicket = ticket }, user, new FakeCurrentTenant(TenantA));

        var result = await svc.UpdateStatusAsync(ticket.Id, new UpdateTicketStatusRequest(TicketStatus.Resolved));

        Assert.True(result.IsSuccess);
        Assert.Equal(TicketStatus.Resolved, result.Value.Status);
        Assert.NotNull(result.Value.ResolvedAt);
    }

    [Fact]
    public async Task Create_maps_request_onto_new_ticket_with_tenant_and_defaults()
    {
        var repo = new FakeTicketRepository();
        var collector = new FakeEventCollector();
        var customer = new Customer { TenantId = TenantA, Name = "Priya", Email = "p@x.com" };
        var user = new FakeCurrentUser(Guid.NewGuid(), TenantA, new HashSet<string> { Permissions.TicketsCreate });
        var svc = new TicketService(repo, new FakeCustomerRepository { NextCustomer = customer },
            new FakeSlaPolicyRepository(), new FakeCurrentTenant(TenantA), user,
            new FakeUnitOfWork(), new FakeClock(FixedNow), collector, new FakeAppCache());

        var result = await svc.CreateAsync(ValidRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantA, repo.AddedTicket!.TenantId);
        Assert.Equal(TicketStatus.Open, repo.AddedTicket.Status);

        // Outbox wiring: creating a ticket must raise exactly one TicketCreated domain event.
        var created = Assert.Single(collector.Raised);
        Assert.Equal("ticket.created", created.EventType);
    }

    [Fact]
    public async Task Create_stamps_sla_deadlines_from_matching_policy()
    {
        var repo = new FakeTicketRepository();
        var collector = new FakeEventCollector();
        var customer = new Customer { TenantId = TenantA, Name = "Priya", Email = "p@x.com" };
        var user = new FakeCurrentUser(Guid.NewGuid(), TenantA, new HashSet<string> { Permissions.TicketsCreate });
        var sla = new FakeSlaPolicyRepository
        {
            Policies = new[]
            {
                new SlaPolicy
                {
                    TenantId = TenantA, Name = "High", Priority = TicketPriority.High,
                    ResponseDueInMinutes = 30, ResolutionDueInMinutes = 240
                }
            }
        };
        var svc = new TicketService(repo, new FakeCustomerRepository { NextCustomer = customer }, sla,
            new FakeCurrentTenant(TenantA), user, new FakeUnitOfWork(), new FakeClock(FixedNow), collector,
            new FakeAppCache());

        await svc.CreateAsync(ValidRequest());

        Assert.Equal(FixedNow.AddMinutes(30), repo.AddedTicket!.ResponseDueAt);
        Assert.Equal(FixedNow.AddMinutes(240), repo.AddedTicket.ResolutionDueAt);
        Assert.NotNull(repo.AddedTicket.SlaPolicyId);
    }
}
