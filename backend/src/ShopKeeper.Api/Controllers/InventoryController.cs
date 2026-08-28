namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Application.Common.Dtos;
using ShopKeeper.Application.Inventory.Commands;
using ShopKeeper.Application.Inventory.Dtos;
using ShopKeeper.Application.Inventory.Queries;

[Authorize]
[ApiController]
[Route("api/inventory")]
public class InventoryController(ISender mediator) : ControllerBase
{
    [HttpPost("adjust")]
    public async Task<ActionResult<object>> Adjust(AdjustStockCommand command, CancellationToken ct)
    {
        var quantityOnHand = await mediator.Send(command, ct);
        return Ok(new { quantityOnHand });
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<PagedResult<InventoryTransactionDto>>> GetTransactions(
        [FromQuery] Guid? productId,
        [FromQuery] Guid? branchId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await mediator.Send(new GetInventoryTransactionsQuery(productId, branchId, page, pageSize), ct));

    [HttpGet("stats")]
    public async Task<ActionResult<InventoryStatsDto>> GetStats([FromQuery] Guid? branchId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetInventoryStatsQuery(branchId), ct));
}
