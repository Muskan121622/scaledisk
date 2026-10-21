using ScaleDesk.Application.Common;
using ScaleDesk.Application.Contracts.Customers;

namespace ScaleDesk.Application.Services;

public interface ICustomerService
{
    Task<Result<CustomerResponse>> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default);
    Task<Result<CustomerResponse>> GetAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<CustomerResponse>> ListAsync(CustomerQuery query, CancellationToken ct = default);
    Task<Result<CustomerResponse>> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
