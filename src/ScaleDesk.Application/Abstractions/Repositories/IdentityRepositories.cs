using ScaleDesk.Domain.Entities;

namespace ScaleDesk.Application.Abstractions.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
}

public interface ITenantRepository
{
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Tenant tenant, CancellationToken ct = default);
}

public interface IMembershipRepository
{
    Task<Membership?> GetAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Membership>> ListForUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Membership membership, CancellationToken ct = default);

    /// <summary>Adds or updates depending on whether a prior (e.g. revoked) membership row exists.</summary>
    Task SaveAsync(Membership membership, CancellationToken ct = default);
}

public interface IInvitationRepository
{
    Task<Invitation?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Cross-tenant point lookup by token hash, used by the anonymous accept flow.</summary>
    Task<Invitation?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    Task<Invitation?> FindPendingByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(Invitation invitation, CancellationToken ct = default);
    void Update(Invitation invitation);
    Task<IReadOnlyList<Invitation>> ListPendingAsync(CancellationToken ct = default);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    void Update(RefreshToken token);
}

/// <summary>Portal identity links (global user ↔ tenant customer record). Lookups are always
/// (user, tenant) scoped so a link from one workspace never resolves in another.</summary>
public interface ICustomerUserRepository
{
    Task<CustomerUser?> GetByUserIdAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
    Task<CustomerUser?> GetByCustomerIdAsync(Guid customerId, Guid tenantId, CancellationToken ct = default);
    Task AddAsync(CustomerUser link, CancellationToken ct = default);
}
