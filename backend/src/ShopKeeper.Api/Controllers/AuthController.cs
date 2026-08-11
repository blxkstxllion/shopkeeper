namespace ShopKeeper.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using ShopKeeper.Api.Extensions;
using ShopKeeper.Application.Auth.Commands;
using ShopKeeper.Application.Auth.Dtos;
using ShopKeeper.Application.Auth.Queries;
using ShopKeeper.Application.Common.Interfaces;

[ApiController]
[Route("api/auth")]
public class AuthController(ISender mediator, IWebHostEnvironment env, ICurrentUserService currentUser) : ControllerBase
{
    public record RegisterRequest(string Email, string Password, string FirstName, string LastName);
    public record LoginRequest(string Email, string Password, Guid? BusinessId);
    public record ForgotPasswordRequest(string Email);
    public record ResetPasswordRequest(string Token, string NewPassword);
    public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    public record VerifyEmailRequest(string Token);
    public record SwitchBusinessRequest(Guid BusinessId);
    public record VerifyTwoFactorRequest(string ChallengeToken, string Code);
    public record EnableTwoFactorRequest(string Code);
    public record DisableTwoFactorRequest(string Password);

    [HttpPost("register")]
    public async Task<ActionResult<AuthResultDto>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new RegisterCommand(
            request.Email, request.Password, request.FirstName, request.LastName,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString()), ct);

        Response.SetRefreshTokenCookie(result.RefreshToken, env);
        return Ok(result with { RefreshToken = string.Empty });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new LoginCommand(
            request.Email, request.Password, request.BusinessId,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString()), ct);

        if (result.RequiresTwoFactor)
        {
            return Ok(new { requiresTwoFactor = true, challengeToken = result.ChallengeToken });
        }

        Response.SetRefreshTokenCookie(result.Auth!.RefreshToken, env);
        return Ok(new { requiresTwoFactor = false, auth = result.Auth with { RefreshToken = string.Empty } });
    }

    [HttpPost("2fa/verify")]
    public async Task<ActionResult<AuthResultDto>> VerifyTwoFactor(VerifyTwoFactorRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new VerifyTwoFactorCommand(
            request.ChallengeToken, request.Code,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString()), ct);

        Response.SetRefreshTokenCookie(result.RefreshToken, env);
        return Ok(result with { RefreshToken = string.Empty });
    }

    [Authorize]
    [HttpGet("2fa/status")]
    public async Task<ActionResult<TwoFactorStatusDto>> GetTwoFactorStatus(CancellationToken ct) =>
        Ok(await mediator.Send(new GetTwoFactorStatusQuery(currentUser.UserId!.Value), ct));

    [Authorize]
    [HttpPost("2fa/setup")]
    public async Task<ActionResult<TwoFactorSetupDto>> SetupTwoFactor(CancellationToken ct) =>
        Ok(await mediator.Send(new SetupTwoFactorCommand(currentUser.UserId!.Value), ct));

    [Authorize]
    [HttpPost("2fa/enable")]
    public async Task<ActionResult<object>> EnableTwoFactor(EnableTwoFactorRequest request, CancellationToken ct)
    {
        var recoveryCodes = await mediator.Send(new EnableTwoFactorCommand(currentUser.UserId!.Value, request.Code), ct);
        return Ok(new { recoveryCodes });
    }

    [Authorize]
    [HttpPost("2fa/disable")]
    public async Task<IActionResult> DisableTwoFactor(DisableTwoFactorRequest request, CancellationToken ct)
    {
        await mediator.Send(new DisableTwoFactorCommand(currentUser.UserId!.Value, request.Password), ct);
        return Ok(new { message = "Two-factor authentication has been disabled." });
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResultDto>> Refresh(CancellationToken ct)
    {
        var refreshToken = Request.GetRefreshTokenCookie();
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new { title = "No refresh token was provided." });
        }

        var result = await mediator.Send(new RefreshTokenCommand(
            refreshToken, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString()), ct);

        Response.SetRefreshTokenCookie(result.RefreshToken, env);
        return Ok(result with { RefreshToken = string.Empty });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var refreshToken = Request.GetRefreshTokenCookie();
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await mediator.Send(new LogoutCommand(refreshToken, HttpContext.Connection.RemoteIpAddress?.ToString()), ct);
        }

        Response.ClearRefreshTokenCookie();
        return NoContent();
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await mediator.Send(new ForgotPasswordCommand(request.Email), ct);
        return Ok(new { message = "If an account with that email exists, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await mediator.Send(new ResetPasswordCommand(request.Token, request.NewPassword), ct);
        return Ok(new { message = "Your password has been reset. Please log in." });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await mediator.Send(new ChangePasswordCommand(currentUser.UserId!.Value, request.CurrentPassword, request.NewPassword), ct);
        return Ok(new { message = "Your password has been changed." });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken ct)
    {
        await mediator.Send(new VerifyEmailCommand(request.Token), ct);
        return Ok(new { message = "Your email has been verified." });
    }

    [Authorize]
    [HttpPost("switch-business")]
    public async Task<ActionResult<AuthResultDto>> SwitchBusiness(SwitchBusinessRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new SwitchBusinessCommand(
            currentUser.UserId!.Value, request.BusinessId,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString()), ct);

        Response.SetRefreshTokenCookie(result.RefreshToken, env);
        return Ok(result with { RefreshToken = string.Empty });
    }

    [Authorize]
    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<SessionDto>>> GetSessions(CancellationToken ct) =>
        Ok(await mediator.Send(new GetActiveSessionsQuery(Request.GetRefreshTokenCookie()), ct));

    [Authorize]
    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> RevokeSession(Guid id, CancellationToken ct)
    {
        await mediator.Send(new RevokeSessionCommand(id), ct);
        return NoContent();
    }

    [Authorize]
    [HttpPost("sessions/revoke-others")]
    public async Task<IActionResult> RevokeOtherSessions(CancellationToken ct)
    {
        await mediator.Send(new RevokeAllOtherSessionsCommand(Request.GetRefreshTokenCookie()), ct);
        return NoContent();
    }
}
