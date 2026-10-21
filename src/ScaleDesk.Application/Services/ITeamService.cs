using ScaleDesk.Application.Common;
using ScaleDesk.Application.Contracts.Teams;

namespace ScaleDesk.Application.Services;

public interface ITeamService
{
    Task<Result<TeamResponse>> CreateAsync(CreateTeamRequest request, CancellationToken ct = default);
    Task<Result<TeamResponse>> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TeamResponse>> ListAsync(CancellationToken ct = default);
    Task<Result<TeamResponse>> UpdateAsync(Guid id, UpdateTeamRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
