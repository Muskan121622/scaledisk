using ScaleDesk.Application.Common;
using ScaleDesk.Application.Contracts.Automation;

namespace ScaleDesk.Application.Services;

public interface IAutomationService
{
    Task<IReadOnlyList<AutomationRuleResponse>> ListAsync(CancellationToken ct = default);
    Task<Result<AutomationRuleResponse>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<AutomationRuleResponse>> CreateAsync(CreateAutomationRuleRequest request, CancellationToken ct = default);
    Task<Result<AutomationRuleResponse>> UpdateAsync(Guid id, UpdateAutomationRuleRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
