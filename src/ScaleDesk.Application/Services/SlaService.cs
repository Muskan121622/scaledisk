using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Abstractions.Repositories;
using ScaleDesk.Application.Common;
using ScaleDesk.Application.Common.Caching;
using ScaleDesk.Application.Contracts.Sla;
using ScaleDesk.Application.Exceptions;
using ScaleDesk.Domain.Constants;
using ScaleDesk.Domain.Entities;

namespace ScaleDesk.Application.Services;

/// <summary>
/// Manages a tenant's SLA policies (spec §9). Writes gate on <c>sla.manage</c>. Exactly one policy
/// may be the workspace default; promoting one demotes the rest so deadline resolution is unambiguous.
/// </summary>
public sealed class SlaService : ISlaService
{
    private readonly ISlaPolicyRepository _policies;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly IAppCache _cache;

    public SlaService(
        ISlaPolicyRepository policies,
        ICurrentTenant tenant,
        ICurrentUser user,
        IUnitOfWork uow,
        IClock clock,
        IAppCache cache)
    {
        _policies = policies;
        _tenant = tenant;
        _user = user;
        _uow = uow;
        _clock = clock;
        _cache = cache;
    }

    public async Task<Result<SlaPolicyResponse>> CreateAsync(CreateSlaPolicyRequest request, CancellationToken ct = default)
    {
        RequirePermission(Permissions.SlaManage);
        var tenantId = _tenant.RequireTenant();
        Validate(request.Name, request.ResponseDueInMinutes, request.ResolutionDueInMinutes);

        if (request.IsDefault)
            await ClearDefaultsAsync(ct);

        var policy = new SlaPolicy
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Priority = request.Priority,
            ResponseDueInMinutes = request.ResponseDueInMinutes,
            ResolutionDueInMinutes = request.ResolutionDueInMinutes,
            IsDefault = request.IsDefault,
            CreatedAt = _clock.UtcNow
        };

        await _policies.AddAsync(policy, ct);
        await _uow.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.SlaPolicies(tenantId), ct);

        return Result.Ok(Map(policy));
    }

    public async Task<IReadOnlyList<SlaPolicyResponse>> ListAsync(CancellationToken ct = default)
    {
        RequirePermission(Permissions.SlaManage);
        var policies = await _policies.ListAsync(ct);
        return policies.Select(Map).ToList();
    }

    public async Task<Result<SlaPolicyResponse>> UpdateAsync(Guid id, UpdateSlaPolicyRequest request, CancellationToken ct = default)
    {
        RequirePermission(Permissions.SlaManage);
        var policy = await GetOwnedAsync(id, ct);
        Validate(request.Name, request.ResponseDueInMinutes, request.ResolutionDueInMinutes);

        if (request.IsDefault && !policy.IsDefault)
            await ClearDefaultsAsync(ct);

        policy.Name = request.Name.Trim();
        policy.Priority = request.Priority;
        policy.ResponseDueInMinutes = request.ResponseDueInMinutes;
        policy.ResolutionDueInMinutes = request.ResolutionDueInMinutes;
        policy.IsDefault = request.IsDefault;
        policy.UpdatedAt = _clock.UtcNow;

        _policies.Update(policy);
        await _uow.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.SlaPolicies(policy.TenantId), ct);

        return Result.Ok(Map(policy));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        RequirePermission(Permissions.SlaManage);
        var policy = await GetOwnedAsync(id, ct);

        _policies.Remove(policy);
        await _uow.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.SlaPolicies(policy.TenantId), ct);

        return Result.Ok();
    }

    private async Task<SlaPolicy> GetOwnedAsync(Guid id, CancellationToken ct) =>
        await _policies.GetAsync(id, ct)
            ?? throw new NotFoundException("SLA policy not found in this workspace.");

    /// <summary>At most one default per tenant; demote any existing default before promoting a new one.</summary>
    private async Task ClearDefaultsAsync(CancellationToken ct)
    {
        var all = await _policies.ListAsync(ct);
        foreach (var existing in all.Where(p => p.IsDefault))
        {
            existing.IsDefault = false;
            _policies.Update(existing);
        }
    }

    private void RequirePermission(string permission)
    {
        if (!_user.HasPermission(permission))
            throw new ForbiddenException($"Missing required permission '{permission}'.");
    }

    private static void Validate(string name, int responseMinutes, int resolutionMinutes)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name)) errors["name"] = ["Policy name is required."];
        if (responseMinutes <= 0) errors["responseDueInMinutes"] = ["Must be a positive number of minutes."];
        if (resolutionMinutes <= 0) errors["resolutionDueInMinutes"] = ["Must be a positive number of minutes."];
        if (errors.Count > 0) throw new ValidationException(errors);
    }

    private static SlaPolicyResponse Map(SlaPolicy p) => new(
        p.Id, p.Name, p.Priority, p.ResponseDueInMinutes, p.ResolutionDueInMinutes, p.IsDefault);
}
