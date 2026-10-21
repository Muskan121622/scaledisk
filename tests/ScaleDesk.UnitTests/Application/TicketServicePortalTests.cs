using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Contracts.Tickets;
using ScaleDesk.Application.Exceptions;
using ScaleDesk.Application.Services;
using ScaleDesk.Domain.Constants;
using ScaleDesk.Domain.Enums;
using ScaleDesk.Domain.Entities;
using ScaleDesk.UnitTests.TestDoubles;

namespace ScaleDesk.UnitTests.Application;

/// <summary>
/// Portal branches of TicketService: customers are gated on OWNERSHIP (cid claim) instead of
/// permissions, may never self-route/self-assign, never see internal notes, and may only
/// confirm closure of their own Resolved tickets.
/// </summary>
public class TicketServicePortalTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid CustomerMe = Guid.NewGuid();
    private static readonly Guid CustomerOther = Guid.NewGuid();
    private static readonly DateTime FixedNow = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    private static FakeCurrentUser PortalUser() =>
        new(Guid.NewGuid(), TenantA, new HashSet<string>(), customerId: CustomerMe); // zero permissions by design

    private static TicketService Build(FakeTicketRepository tickets, ICurrentUser user, Customer? customer = null)
        => new(tickets, new FakeCustomerRepository { NextCustomer = customer }, new FakeSlaPolicyRepository(),
            new FakeCurrentTenant(TenantA), user, new FakeUnitOfWork(), new FakeClock(FixedNow),
            new FakeEventCollector(), new FakeAppCache());

    private static Ticket TicketOwnedBy(Guid customerId, TicketStatus status = TicketStatus.Open) =>
        new() { TenantId = TenantA, CustomerId = customerId, Subject = "x", Description = "y", Status = status };

    [Fact]
    public async Task Portal_create_needs_no_permission_and_pins_customer_from_claim()
    {
        var repo = new FakeTicketRepository();
        var customer = new Customer { Id = CustomerMe, TenantId = TenantA, Name = "Priya", Email = "p@x.com" };
        var svc = Build(repo, PortalUser(), customer);

        // Body carries a foreign CustomerId + self-assignment attempts — all must be overridden/ignored.
        var result = await svc.CreateAsync(new CreateTicketRequest(
            CustomerOther, "Printer offline", "No printing", TicketPriority.Medium, TicketCategory.Technical,
            TeamId: Guid.NewGuid(), AssigneeUserId: Guid.NewGuid()));

        Assert.True(result.IsSuccess);
        Assert.Equal(CustomerMe, repo.AddedTicket!.CustomerId);
        Assert.Null(repo.AddedTicket.TeamId);
        Assert.Null(repo.AddedTicket.AssigneeUserId);
    }

    [Fact]
    public async Task Portal_get_foreign_ticket_is_not_found_not_forbidden()
    {
        // Same answer shape as a cross-tenant id: the customer learns nothing about others' rows.
        var repo = new FakeTicketRepository { NextTicket = TicketOwnedBy(CustomerOther) };
        var svc = Build(repo, PortalUser());

        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Portal_get_own_ticket_succeeds_without_any_permissions()
    {
        var repo = new FakeTicketRepository { NextTicket = TicketOwnedBy(CustomerMe) };
        var svc = Build(repo, PortalUser());

        var result = await svc.GetAsync(repo.NextTicket!.Id);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Portal_reply_is_attributed_to_customer_and_never_internal()
    {
        var repo = new FakeTicketRepository { NextTicket = TicketOwnedBy(CustomerMe) };
        var svc = Build(repo, PortalUser());

        var result = await svc.AddCommentAsync(repo.NextTicket!.Id, new AddTicketCommentRequest("Still broken", true));

        Assert.True(result.IsSuccess);
        Assert.Null(repo.AddedComment!.AuthorUserId);
        Assert.Equal(CustomerMe, repo.AddedComment.CustomerId);
        Assert.False(repo.AddedComment.IsInternal); // IsInternal is structurally impossible from the portal
    }

    [Fact]
    public async Task Portal_comment_list_hides_internal_notes()
    {
        var ticket = TicketOwnedBy(CustomerMe);
        var repo = new FakeTicketRepository
        {
            NextTicket = ticket,
            Comments = new[]
            {
                new TicketComment { TicketId = ticket.Id, Body = "agent reply", IsInternal = false, AuthorUserId = Guid.NewGuid() },
                new TicketComment { TicketId = ticket.Id, Body = "secret escalation", IsInternal = true, AuthorUserId = Guid.NewGuid() }
            }
        };
        var svc = Build(repo, PortalUser());

        var comments = await svc.GetCommentsAsync(ticket.Id);
        Assert.All(comments, c => Assert.False(c.IsInternal));
        Assert.Single(comments);
    }

    [Fact]
    public async Task Staff_session_cannot_use_the_customer_close_endpoint()
    {
        var user = new FakeCurrentUser(Guid.NewGuid(), TenantA, new HashSet<string> { Permissions.TicketsUpdate });
        var repo = new FakeTicketRepository { NextTicket = TicketOwnedBy(CustomerOther, TicketStatus.Resolved) };
        var svc = Build(repo, user);

        await Assert.ThrowsAsync<ForbiddenException>(() => svc.CloseFromCustomerAsync(repo.NextTicket!.Id));
    }

    [Fact]
    public async Task Close_is_only_allowed_on_resolved_tickets()
    {
        var repo = new FakeTicketRepository { NextTicket = TicketOwnedBy(CustomerMe, TicketStatus.InProgress) };
        var svc = Build(repo, PortalUser());

        var result = await svc.CloseFromCustomerAsync(repo.NextTicket!.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("not_resolved", result.ErrorCode);
    }

    [Fact]
    public async Task Closing_own_resolved_ticket_marks_it_closed()
    {
        var repo = new FakeTicketRepository { NextTicket = TicketOwnedBy(CustomerMe, TicketStatus.Resolved) };
        var svc = Build(repo, PortalUser());

        var result = await svc.CloseFromCustomerAsync(repo.NextTicket!.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(TicketStatus.Closed, result.Value.Status);
    }

    [Fact]
    public async Task Staff_create_is_still_permission_gated()
    {
        // Regression guard: the portal branch must not have loosened the staff path.
        var user = new FakeCurrentUser(Guid.NewGuid(), TenantA, new HashSet<string>());
        var svc = Build(new FakeTicketRepository(), user);

        await Assert.ThrowsAsync<ForbiddenException>(() => svc.CreateAsync(
            new CreateTicketRequest(CustomerMe, "s", "d", TicketPriority.Low, TicketCategory.General, null, null)));
    }
}
