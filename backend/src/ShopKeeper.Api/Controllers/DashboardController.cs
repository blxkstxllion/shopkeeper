namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Application.Dashboard.Dtos;
using ShopKeeper.Application.Dashboard.Queries;

[Authorize]
[ApiController]
[Route("api/dashboard")]
public class DashboardController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary([FromQuery] Guid? branchId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetDashboardSummaryQuery(branchId), ct));
}
