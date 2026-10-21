using ScaleDesk.Application.Common;
using ScaleDesk.Application.Contracts.Invitations;

namespace ScaleDesk.Application.Services;

public interface IInvitationService
{
    Task<Result<InvitationResponse>> InviteAsync(CreateInvitationRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<InvitationResponse>> ListPendingAsync(CancellationToken ct = default);
    Task<Result> RevokeAsync(Guid id, CancellationToken ct = default);
    Task<Result<AcceptInvitationResponse>> AcceptAsync(AcceptInvitationRequest request, CancellationToken ct = default);
}
