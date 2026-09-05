namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Application.Common.Dtos;
using ShopKeeper.Application.Products.Commands;
using ShopKeeper.Application.Products.Dtos;
using ShopKeeper.Application.Products.Queries;

[Authorize]
[ApiController]
[Route("api/products")]
public class ProductsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? branchId,
        [FromQuery] bool activeOnly = true,
        [FromQuery] bool lowStockOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await mediator.Send(new GetProductsQuery(search, categoryId, branchId, activeOnly, lowStockOnly, page, pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> GetById(Guid id, [FromQuery] Guid? branchId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetProductByIdQuery(id, branchId), ct));

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(CreateProductCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateProductCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest();
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? clientRequestId, CancellationToken ct)
    {
        await mediator.Send(new DeleteProductCommand(id, clientRequestId), ct);
        return NoContent();
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<ProductCategoryDto>>> GetCategories(CancellationToken ct) =>
        Ok(await mediator.Send(new GetProductCategoriesQuery(), ct));

    [HttpPost("categories")]
    public async Task<ActionResult<ProductCategoryDto>> CreateCategory(CreateProductCategoryCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command, ct));
}
