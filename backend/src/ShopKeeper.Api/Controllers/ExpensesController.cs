namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Application.Common.Dtos;
using ShopKeeper.Application.Expenses.Commands;
using ShopKeeper.Application.Expenses.Dtos;
using ShopKeeper.Application.Expenses.Queries;

[Authorize]
[ApiController]
[Route("api/expenses")]
public class ExpensesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ExpenseDto>>> GetAll(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? branchId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await mediator.Send(new GetExpensesQuery(from, to, categoryId, branchId, page, pageSize), ct));

    [HttpPost]
    public async Task<ActionResult<ExpenseDto>> Create(CreateExpenseCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateExpenseCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest();
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? clientRequestId, CancellationToken ct)
    {
        await mediator.Send(new DeleteExpenseCommand(id, clientRequestId), ct);
        return NoContent();
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<ExpenseCategoryDto>>> GetCategories(CancellationToken ct) =>
        Ok(await mediator.Send(new GetExpenseCategoriesQuery(), ct));

    [HttpPost("categories")]
    public async Task<ActionResult<ExpenseCategoryDto>> CreateCategory(CreateExpenseCategoryCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command, ct));
}
