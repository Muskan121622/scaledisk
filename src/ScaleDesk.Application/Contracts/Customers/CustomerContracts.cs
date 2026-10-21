namespace ScaleDesk.Application.Contracts.Customers;

public sealed record CreateCustomerRequest(
    string Name,
    string Email,
    string? Phone,
    string? Company,
    IReadOnlyList<string>? Tags,
    string? Metadata);

public sealed record UpdateCustomerRequest(
    string Name,
    string Email,
    string? Phone,
    string? Company,
    IReadOnlyList<string>? Tags,
    string? Metadata);

public sealed record CustomerResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string Email,
    string? Phone,
    string? Company,
    IReadOnlyList<string> Tags,
    string? Metadata,
    DateTime CreatedAt);

public sealed record CustomerQuery(
    string? Search,
    int Page = 1,
    int PageSize = 20);
