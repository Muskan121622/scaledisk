using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Abstractions.Repositories;
using ScaleDesk.Application.Common;
using ScaleDesk.Application.Contracts.Customers;
using ScaleDesk.Application.Exceptions;
using ScaleDesk.Domain.Constants;
using ScaleDesk.Domain.Entities;
using ScaleDesk.Domain.Events;

namespace ScaleDesk.Application.Services;

/// <summary>
/// Tenant-scoped customer CRM use-cases. Reads gate on <c>customers.read</c>; all mutations gate on
/// the coarser <c>customers.update</c> capability (Agents are read-only on customers by design).
/// Tenant binding + the global query filter make a foreign customer id unresolvable here.
/// </summary>
public sealed class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customers;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly IEventCollector _events;

    public CustomerService(
        ICustomerRepository customers,
        ICurrentTenant tenant,
        ICurrentUser user,
        IUnitOfWork uow,
        IClock clock,
        IEventCollector events)
    {
        _customers = customers;
        _tenant = tenant;
        _user = user;
        _uow = uow;
        _clock = clock;
        _events = events;
    }

    public async Task<Result<CustomerResponse>> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        RequirePermission(Permissions.CustomersUpdate);
        var tenantId = _tenant.RequireTenant();
        var email = NormalizeEmail(request.Email);
        EnsureName(request.Name);

        if (await _customers.FindByEmailAsync(email, ct) is not null)
            throw new ConflictException($"A customer with email '{email}' already exists in this workspace.");

        var customer = new Customer
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Email = email,
            Phone = TrimOrNull(request.Phone),
            Company = TrimOrNull(request.Company),
            Tags = NormalizeTags(request.Tags),
            Metadata = request.Metadata,
            CreatedAt = _clock.UtcNow
        };

        await _customers.AddAsync(customer, ct);
        _events.Raise(new CustomerCreated(tenantId, customer.Id));
        await _uow.SaveChangesAsync(ct);

        return Result.Ok(Map(customer));
    }

    public async Task<Result<CustomerResponse>> GetAsync(Guid id, CancellationToken ct = default)
    {
        RequirePermission(Permissions.CustomersRead);
        var customer = await GetOwnedAsync(id, ct);
        return Result.Ok(Map(customer));
    }

    public async Task<PagedResult<CustomerResponse>> ListAsync(CustomerQuery query, CancellationToken ct = default)
    {
        RequirePermission(Permissions.CustomersRead);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var search = TrimOrNull(query.Search);

        var items = await _customers.ListAsync(search, page, pageSize, ct);
        var total = await _customers.CountAsync(search, ct);

        return new PagedResult<CustomerResponse>(items.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<Result<CustomerResponse>> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct = default)
    {
        RequirePermission(Permissions.CustomersUpdate);
        var customer = await GetOwnedAsync(id, ct);
        var email = NormalizeEmail(request.Email);
        EnsureName(request.Name);

        var existing = await _customers.FindByEmailAsync(email, ct);
        if (existing is not null && existing.Id != customer.Id)
            throw new ConflictException($"Another customer already uses email '{email}'.");

        customer.Name = request.Name.Trim();
        customer.Email = email;
        customer.Phone = TrimOrNull(request.Phone);
        customer.Company = TrimOrNull(request.Company);
        customer.Tags = NormalizeTags(request.Tags);
        customer.Metadata = request.Metadata;
        customer.UpdatedAt = _clock.UtcNow;

        _customers.Update(customer);
        await _uow.SaveChangesAsync(ct);

        return Result.Ok(Map(customer));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        RequirePermission(Permissions.CustomersUpdate);
        var customer = await GetOwnedAsync(id, ct);

        // Tickets restrict-delete FK to customers: a customer with tickets surfaces as a DB
        // constraint conflict via the exception middleware rather than silently orphaning history.
        _customers.Remove(customer);
        await _uow.SaveChangesAsync(ct);

        return Result.Ok();
    }

    private async Task<Customer> GetOwnedAsync(Guid id, CancellationToken ct) =>
        await _customers.GetAsync(id, ct)
            ?? throw new NotFoundException("Customer not found in this workspace.");

    private void RequirePermission(string permission)
    {
        if (!_user.HasPermission(permission))
            throw new ForbiddenException($"Missing required permission '{permission}'.");
    }

    private static void EnsureName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("name", "Customer name is required.");
    }

    private static string NormalizeEmail(string email)
    {
        var trimmed = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (trimmed.Length == 0 || !trimmed.Contains('@'))
            throw new ValidationException("email", "A valid email address is required.");
        return trimmed;
    }

    private static List<string> NormalizeTags(IReadOnlyList<string>? tags) =>
        tags is null
            ? new List<string>()
            : tags.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct().Take(50).ToList();

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CustomerResponse Map(Customer c) => new(
        c.Id, c.TenantId, c.Name, c.Email, c.Phone, c.Company, c.Tags, c.Metadata, c.CreatedAt);
}
