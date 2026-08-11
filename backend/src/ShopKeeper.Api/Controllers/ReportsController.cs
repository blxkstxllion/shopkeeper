namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Application.Reports.Dtos;
using ShopKeeper.Application.Reports.Queries;

[Authorize]
[ApiController]
[Route("api/reports")]
public class ReportsController(ISender mediator) : ControllerBase
{
    [HttpGet("profitability")]
    public async Task<ActionResult<ProfitabilityReportDto>> GetProfitability(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? branchId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetProfitabilityReportQuery(from, to, branchId), ct));

    [HttpGet("expenses")]
    public async Task<ActionResult<ExpenseReportDto>> GetExpenses(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? branchId, [FromQuery] Guid? categoryId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetExpenseReportQuery(from, to, branchId, categoryId), ct));

    [HttpGet("inventory")]
    public async Task<ActionResult<InventoryReportDto>> GetInventory(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? branchId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetInventoryReportQuery(from, to, branchId), ct));
}
