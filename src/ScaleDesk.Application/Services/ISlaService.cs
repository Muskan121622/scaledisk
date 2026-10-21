using ScaleDesk.Application.Common;
using ScaleDesk.Application.Contracts.Sla;

namespace ScaleDesk.Application.Services;

public interface ISlaService
{
    Task<Result<SlaPolicyResponse>> CreateAsync(CreateSlaPolicyRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<SlaPolicyResponse>> ListAsync(CancellationToken ct = default);
    Task<Result<SlaPolicyResponse>> UpdateAsync(Guid id, UpdateSlaPolicyRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
