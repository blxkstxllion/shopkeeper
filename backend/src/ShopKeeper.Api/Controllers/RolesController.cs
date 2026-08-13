namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Application.Employees.Dtos;
using ShopKeeper.Application.Employees.Queries;

[Authorize]
[ApiController]
[Route("api/roles")]
public class RolesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetAll(CancellationToken ct) =>
        Ok(await mediator.Send(new GetRolesQuery(), ct));
}
