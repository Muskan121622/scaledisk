using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScaleDesk.Api.Common;
using ScaleDesk.Application.Contracts.Customers;
using ScaleDesk.Application.Services;
using ScaleDesk.Domain.Constants;

namespace ScaleDesk.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize]
[Produces("application/json")]
public sealed class CustomersController(ICustomerService customers) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.CustomersRead)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new CustomerQuery(search, page, pageSize);
        return Ok(await customers.ListAsync(query, ct));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.CustomersRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => (await customers.GetAsync(id, ct)).ToActionResult();

    [HttpPost]
    [Authorize(Policy = Permissions.CustomersUpdate)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        var result = await customers.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value)
            : result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.CustomersUpdate)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken ct)
        => (await customers.UpdateAsync(id, request, ct)).ToActionResult();

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.CustomersUpdate)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await customers.DeleteAsync(id, ct)).ToActionResult();
}
