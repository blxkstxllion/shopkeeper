namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}
