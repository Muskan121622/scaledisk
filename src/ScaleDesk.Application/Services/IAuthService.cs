using ScaleDesk.Application.Common;
using ScaleDesk.Application.Contracts.Auth;
using ScaleDesk.Application.Contracts.Platform;

namespace ScaleDesk.Application.Services;

public interface IAuthService
{
    Task<Result<AuthTokensResponse>> RegisterAsync(RegisterRequest request, string? clientIp, CancellationToken ct = default);
    Task<Result<AuthTokensResponse>> LoginAsync(LoginRequest request, string? clientIp, CancellationToken ct = default);
    Task<Result<PlatformSessionResponse>> PlatformLoginAsync(PlatformLoginRequest request, CancellationToken ct = default);
    Task<Result<AuthTokensResponse>> RefreshAsync(RefreshRequest request, string? clientIp, CancellationToken ct = default);
    Task<Result> LogoutAsync(string refreshToken, CancellationToken ct = default);
    Task<Result<CurrentUserResponse>> GetCurrentUserAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
}
