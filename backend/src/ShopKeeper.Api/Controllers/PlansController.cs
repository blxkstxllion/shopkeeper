namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Application.Plans.Commands;
using ShopKeeper.Application.Plans.Dtos;
using ShopKeeper.Application.Plans.Queries;

[Authorize]
[ApiController]
[Route("api/plans")]
public class PlansController(ISender mediator) : ControllerBase
{
    [HttpGet("usage")]
    public async Task<ActionResult<PlanUsageDto>> GetUsage(CancellationToken ct) =>
        Ok(await mediator.Send(new GetPlanUsageQuery(), ct));

    [HttpPost("tier")]
    public async Task<IActionResult> SetTier([FromBody] SetPlanTierCommand command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpPost("inventory-add-on")]
    public async Task<IActionResult> SetInventoryAddOn([FromBody] SetInventoryAddOnCommand command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutSessionDto>> InitiateCheckout(
        [FromBody] InitiateCheckoutCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command, ct));

    [HttpPost("checkout/verify")]
    public async Task<ActionResult<VerifyCheckoutResultDto>> VerifyCheckout(
        [FromBody] VerifyCheckoutCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command, ct));
}
