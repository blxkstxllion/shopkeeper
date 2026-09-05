namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Api.Extensions;
using ShopKeeper.Application.Auth.Dtos;
using ShopKeeper.Application.Employees.Commands;
using ShopKeeper.Application.Employees.Dtos;
using ShopKeeper.Application.Employees.Queries;

[ApiController]
[Route("api/employees")]
public class EmployeesController(ISender mediator, IWebHostEnvironment env) : ControllerBase
{
    public record InviteRequest(string Email, Guid RoleId, Guid? BranchId, Guid? ClientRequestId = null);
    public record AcceptInvitationRequest(string Password, string FirstName, string LastName);
    public record ApproveJoinRequestRequest(Guid RoleId, Guid? BranchId, Guid? ClientRequestId = null);
    public record RejectJoinRequestRequest(Guid? ClientRequestId = null);

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<BusinessUsersDto>> GetAll(CancellationToken ct) =>
        Ok(await mediator.Send(new GetBusinessUsersQuery(), ct));

    [Authorize]
    [HttpPost("invite")]
    public async Task<IActionResult> Invite(InviteRequest request, CancellationToken ct)
    {
        await mediator.Send(new InviteEmployeeCommand(request.Email, request.RoleId, request.BranchId, request.ClientRequestId), ct);
        return Ok(new { message = "Invitation sent." });
    }

    [Authorize]
    [HttpDelete("{businessUserId:guid}")]
    public async Task<IActionResult> Remove(Guid businessUserId, [FromQuery] Guid? clientRequestId, CancellationToken ct)
    {
        await mediator.Send(new RemoveEmployeeCommand(businessUserId, clientRequestId), ct);
        return NoContent();
    }

    [HttpGet("invitations/{token}")]
    public async Task<ActionResult<InvitationDetailsDto>> GetInvitation(string token, CancellationToken ct) =>
        Ok(await mediator.Send(new GetInvitationByTokenQuery(token), ct));

    [HttpPost("invitations/{token}/accept")]
    public async Task<ActionResult<AuthResultDto>> AcceptInvitation(string token, AcceptInvitationRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new AcceptInvitationCommand(
            token, request.Password, request.FirstName, request.LastName,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString()), ct);

        Response.SetRefreshTokenCookie(result.RefreshToken, env, result.RememberMe);
        return Ok(result with { RefreshToken = string.Empty });
    }

    [Authorize]
    [HttpPost("invitations/{token}/accept-existing")]
    public async Task<ActionResult<AuthResultDto>> AcceptInvitationForExistingUser(string token, CancellationToken ct)
    {
        var result = await mediator.Send(new AcceptInvitationForExistingUserCommand(
            token, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString()), ct);

        Response.SetRefreshTokenCookie(result.RefreshToken, env, result.RememberMe);
        return Ok(result with { RefreshToken = string.Empty });
    }

    [Authorize]
    [HttpGet("join-code")]
    public async Task<ActionResult<object>> GetJoinCode(CancellationToken ct) =>
        Ok(new { code = await mediator.Send(new GetJoinCodeQuery(), ct) });

    [Authorize]
    [HttpPost("join-code/regenerate")]
    public async Task<ActionResult<object>> RegenerateJoinCode(CancellationToken ct) =>
        Ok(new { code = await mediator.Send(new RegenerateJoinCodeCommand(), ct) });

    [Authorize]
    [HttpDelete("join-code")]
    public async Task<IActionResult> RevokeJoinCode(CancellationToken ct)
    {
        await mediator.Send(new RevokeJoinCodeCommand(), ct);
        return NoContent();
    }

    [Authorize]
    [HttpPost("join-requests/{id:guid}/approve")]
    public async Task<IActionResult> ApproveJoinRequest(Guid id, ApproveJoinRequestRequest request, CancellationToken ct)
    {
        await mediator.Send(new ApproveJoinRequestCommand(id, request.RoleId, request.BranchId, request.ClientRequestId), ct);
        return NoContent();
    }

    [Authorize]
    [HttpPost("join-requests/{id:guid}/reject")]
    public async Task<IActionResult> RejectJoinRequest(Guid id, RejectJoinRequestRequest request, CancellationToken ct)
    {
        await mediator.Send(new RejectJoinRequestCommand(id, request.ClientRequestId), ct);
        return NoContent();
    }
}
