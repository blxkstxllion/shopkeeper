namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Application.About.Commands;
using ShopKeeper.Application.About.Dtos;
using ShopKeeper.Application.About.Queries;

[Authorize]
[ApiController]
[Route("api/about")]
public class AboutController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BusinessAboutDto>> Get(CancellationToken ct) =>
        Ok(await mediator.Send(new GetBusinessAboutQuery(), ct));

    [HttpPut]
    public async Task<IActionResult> Update(UpdateBusinessAboutCommand command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        return NoContent();
    }
}
