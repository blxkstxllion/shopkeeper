namespace ShopKeeper.Api.Local.Controllers;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Application.LocalEdition.Commands;
using ShopKeeper.Application.LocalEdition.Dtos;
using ShopKeeper.Application.LocalEdition.Queries;

/// <summary>Offline-edition-only, deliberately has no [Authorize]: LocalAuthenticationHandler
/// already authenticates every request as the single local owner regardless of PIN-lock state,
/// so there's no "anonymous vs authenticated" distinction to enforce here at all - see
/// UnlockLocalAppCommand's doc comment for why the PIN is a UI gate, not an API boundary.</summary>
[ApiController]
[Route("api/local")]
public class LocalController(ISender mediator) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<LocalStatusDto>> GetStatus(CancellationToken ct) =>
        Ok(await mediator.Send(new GetLocalStatusQuery(), ct));

    [HttpPost("setup")]
    public async Task<IActionResult> Setup([FromBody] CompleteLocalSetupCommand command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpPost("unlock")]
    public async Task<ActionResult<bool>> Unlock([FromBody] UnlockLocalAppCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command, ct));
}
