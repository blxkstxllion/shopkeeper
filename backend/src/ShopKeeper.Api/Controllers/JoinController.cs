namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShopKeeper.Application.Employees.Commands;
using ShopKeeper.Application.Employees.Dtos;
using ShopKeeper.Application.Employees.Queries;

[ApiController]
[Route("api/join")]
public class JoinController(ISender mediator) : ControllerBase
{
    public record SubmitJoinRequestRequest(string FirstName, string LastName, string Email, string Phone, string Password);

    [EnableRateLimiting("auth")]
    [HttpGet("{code}")]
    public async Task<ActionResult<JoinBusinessDto>> GetBusiness(string code, CancellationToken ct) =>
        Ok(await mediator.Send(new GetBusinessByJoinCodeQuery(code), ct));

    [EnableRateLimiting("auth")]
    [HttpPost("{code}/request")]
    public async Task<IActionResult> SubmitRequest(string code, SubmitJoinRequestRequest request, CancellationToken ct)
    {
        await mediator.Send(new SubmitJoinRequestCommand(
            code, request.FirstName, request.LastName, request.Email, request.Phone, request.Password), ct);
        return Ok(new { message = "Your request has been submitted and is waiting for approval." });
    }

    [Authorize]
    [HttpPost("{code}/request-existing")]
    public async Task<IActionResult> RequestForExistingUser(string code, CancellationToken ct)
    {
        await mediator.Send(new SubmitJoinRequestForExistingUserCommand(code), ct);
        return Ok(new { message = "Your request has been submitted and is waiting for approval." });
    }
}
