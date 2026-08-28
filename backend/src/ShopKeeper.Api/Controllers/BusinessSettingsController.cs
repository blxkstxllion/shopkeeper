namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}
