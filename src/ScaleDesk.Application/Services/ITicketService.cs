using ScaleDesk.Application.Common;
using ScaleDesk.Application.Contracts.Tickets;

namespace ScaleDesk.Application.Services;

public interface ITicketService
{
    Task<Result<TicketResponse>> CreateAsync(CreateTicketRequest request, CancellationToken ct = default);
    Task<Result<TicketResponse>> GetAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<TicketResponse>> ListAsync(TicketQuery query, CancellationToken ct = default);
    Task<Result<TicketResponse>> UpdateStatusAsync(Guid id, UpdateTicketStatusRequest request, CancellationToken ct = default);
    Task<Result<TicketResponse>> AssignAsync(Guid id, AssignTicketRequest request, CancellationToken ct = default);
    Task<Result<TicketCommentResponse>> AddCommentAsync(Guid id, AddTicketCommentRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TicketCommentResponse>> GetCommentsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TicketEventResponse>> GetEventsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Portal-only: customer confirms closure of their own Resolved ticket. Staff sessions are rejected.</summary>
    Task<Result<TicketResponse>> CloseFromCustomerAsync(Guid id, CancellationToken ct = default);
}
