namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Application.Common.Dtos;
using ShopKeeper.Application.Sales.Commands;
using ShopKeeper.Application.Sales.Dtos;
using ShopKeeper.Application.Sales.Queries;

[Authorize]
[ApiController]
[Route("api/sales")]
public class SalesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<SaleListItemDto>>> GetAll(
        [FromQuery] Guid? branchId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? customerId = null,
        CancellationToken ct = default) =>
        Ok(await mediator.Send(new GetSalesQuery(branchId, from, to, status, page, pageSize, customerId), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SaleDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetSaleByIdQuery(id), ct));

    [HttpPost]
    public async Task<ActionResult<SaleDto>> Create(CreateSaleCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/void")]
    public async Task<IActionResult> Void(Guid id, [FromBody] VoidSaleRequest body, CancellationToken ct)
    {
        await mediator.Send(new VoidSaleCommand(id, body.Reason, body.ClientRequestId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/refund")]
    public async Task<ActionResult<RefundDto>> Refund(Guid id, [FromBody] RefundSaleRequest body, CancellationToken ct) =>
        Ok(await mediator.Send(new RefundSaleCommand(id, body.Items, body.Reason, body.ClientRequestId), ct));

    [HttpGet("sellable-products")]
    public async Task<ActionResult<IReadOnlyList<SellableProductDto>>> GetSellableProducts(
        [FromQuery] Guid branchId, [FromQuery] string? search, [FromQuery] Guid? categoryId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetSellableProductsQuery(branchId, search, categoryId), ct));
}

public record VoidSaleRequest(string Reason, Guid? ClientRequestId = null);

public record RefundSaleRequest(IReadOnlyList<RefundLineInput> Items, string Reason, Guid? ClientRequestId = null);
