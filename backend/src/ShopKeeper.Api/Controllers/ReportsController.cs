namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Reports.Commands;
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

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? branchId,
        [FromQuery] ReportExportFormat format, CancellationToken ct)
    {
        var result = await mediator.Send(new GenerateBusinessReportCommand(from, to, branchId, format), ct);
        return File(result.Content, result.ContentType, result.FileName);
    }
}
