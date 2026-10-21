using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Application.Contracts.Platform;

/// <summary>A tenant row as seen from ScaleDesk HQ — cross-tenant by design, counts included.</summary>
public sealed record TenantAdminResponse(
    Guid Id,
    string Name,
    string Slug,
    TenantStatus Status,
    string? PlanTier,
    DateTime CreatedAt,
    int UserCount,
    int TicketCount,
    int OpenTicketCount);

/// <summary>Platform-wide counters for the HQ overview screen.</summary>
public sealed record PlatformStatsResponse(
    int TotalTenants,
    int ActiveTenants,
    int SuspendedTenants,
    long TotalUsers,
    long TotalTickets,
    long OpenTickets);

public sealed record PlatformLoginRequest(string Email, string Password);

/// <summary>
/// Access-only session (no refresh lineage by design — HQ sessions are short-lived enough to
/// re-authenticate, which keeps the refresh-rotation store strictly tenant-bound).
/// </summary>
public sealed record PlatformSessionResponse(
    string Email,
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    int ExpiresInSeconds,
    string TokenType);
