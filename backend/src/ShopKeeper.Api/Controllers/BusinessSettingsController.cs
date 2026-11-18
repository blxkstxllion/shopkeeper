namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Api.Extensions;
using ShopKeeper.Application.BusinessSettings.Commands;
using ShopKeeper.Application.BusinessSettings.Dtos;
using ShopKeeper.Application.BusinessSettings.Queries;

[Authorize]
[ApiController]
[Route("api/business-settings")]
public class BusinessSettingsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BusinessSettingsDto>> Get(CancellationToken ct) =>
        Ok(await mediator.Send(new GetBusinessSettingsQuery(), ct));

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateBusinessProfileCommand command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpPut("tax")]
    public async Task<IActionResult> UpdateTax(UpdateTaxSettingsCommand command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpPost("deactivate")]
    public async Task<IActionResult> Deactivate(DeactivateBusinessCommand command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        // Ends the caller's own browser session immediately too - every other session scoped
        // to this business was already revoked server-side by the handler, but this one holds
        // a still-valid refresh cookie for the request that just closed the account.
        Response.ClearRefreshTokenCookie();
        return NoContent();
    }
}
