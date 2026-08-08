namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Application.Auth.Dtos;
using ShopKeeper.Application.Auth.Queries;
using ShopKeeper.Application.Common.Interfaces;

[Authorize]
[ApiController]
[Route("api/users")]
public class UsersController(ISender mediator, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        var result = await mediator.Send(new GetCurrentUserQuery(currentUser.UserId!.Value), ct);
        return Ok(result);
    }
}
