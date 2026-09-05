namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Application.Businesses.Commands;
using ShopKeeper.Application.Businesses.Dtos;
using ShopKeeper.Application.Businesses.Queries;

[Authorize]
[ApiController]
[Route("api/branches")]
public class BranchesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> GetAll(CancellationToken ct) =>
        Ok(await mediator.Send(new GetBranchesQuery(), ct));

    [HttpPost]
    public async Task<ActionResult<BranchDto>> Create(CreateBranchCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateBranchCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest();
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? clientRequestId, CancellationToken ct)
    {
        await mediator.Send(new DeleteBranchCommand(id, clientRequestId), ct);
        return NoContent();
    }
}
