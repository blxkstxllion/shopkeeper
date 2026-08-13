namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Application.Suppliers.Commands;
using ShopKeeper.Application.Suppliers.Dtos;
using ShopKeeper.Application.Suppliers.Queries;

[Authorize]
[ApiController]
[Route("api/suppliers")]
public class SuppliersController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupplierDto>>> GetAll(CancellationToken ct) =>
        Ok(await mediator.Send(new GetSuppliersQuery(), ct));

    [HttpPost]
    public async Task<ActionResult<SupplierDto>> Create(CreateSupplierCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateSupplierCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest();
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteSupplierCommand(id), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/restocks")]
    public async Task<ActionResult<IReadOnlyList<SupplierRestockDto>>> GetRestockHistory(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetSupplierRestockHistoryQuery(id), ct));

    [HttpPost("{id:guid}/restock")]
    public async Task<ActionResult<object>> Restock(Guid id, RestockFromSupplierCommand command, CancellationToken ct)
    {
        if (id != command.SupplierId) return BadRequest();
        var quantityOnHand = await mediator.Send(command, ct);
        return Ok(new { quantityOnHand });
    }
}
