namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Application.Employees.Dtos;
using ShopKeeper.Application.Employees.Queries;
using ShopKeeper.Application.Roles.Commands;
using ShopKeeper.Application.Roles.Dtos;
using ShopKeeper.Application.Roles.Queries;

[Authorize]
[ApiController]
[Route("api/roles")]
public class RolesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetAll(CancellationToken ct) =>
        Ok(await mediator.Send(new GetRolesQuery(), ct));

    [HttpGet("management")]
    public async Task<ActionResult<IReadOnlyList<RoleManagementDto>>> GetManagement(CancellationToken ct) =>
        Ok(await mediator.Send(new GetRoleManagementQuery(), ct));

    [HttpGet("permissions")]
    public async Task<ActionResult<IReadOnlyList<PermissionCatalogItemDto>>> GetPermissions(CancellationToken ct) =>
        Ok(await mediator.Send(new GetPermissionCatalogQuery(), ct));

    [HttpPost]
    public async Task<ActionResult<Guid>> Create(CreateRoleCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateRoleCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest();
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteRoleCommand(id), ct);
        return NoContent();
    }
}
