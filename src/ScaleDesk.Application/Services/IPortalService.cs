using ScaleDesk.Application.Common;
using ScaleDesk.Application.Contracts.Auth;
using ScaleDesk.Application.Contracts.Portal;

namespace ScaleDesk.Application.Services;

/// <summary>
/// Customer portal session lifecycle. Lives beside (not inside) IAuthService because the
/// resulting sessions are a different kind: scope=portal + cid claim, no RBAC permissions.
/// Ticket operations are NOT here — the portal reuses <see cref="ITicketService"/> directly so
/// customers go through the same SLA/outbox/automation pipeline as staff.
/// </summary>
public interface IPortalService
{
    Task<Result<AuthTokensResponse>> RegisterAsync(PortalRegisterRequest request, string? clientIp, CancellationToken ct = default);
    Task<Result<AuthTokensResponse>> LoginAsync(PortalLoginRequest request, string? clientIp, CancellationToken ct = default);
    Task<Result<AuthTokensResponse>> RefreshAsync(PortalRefreshRequest request, string? clientIp, CancellationToken ct = default);
    Task<Result> LogoutAsync(string refreshToken, CancellationToken ct = default);
}
