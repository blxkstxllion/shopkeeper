namespace ShopKeeper.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using ShopKeeper.Api.Extensions;
using ShopKeeper.Application.Auth.Commands;
using ShopKeeper.Application.Auth.Dtos;
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

    [HttpPost("register")]
    public async Task<ActionResult<AuthResultDto>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new RegisterCommand(
            request.Email, request.Password, request.FirstName, request.LastName, HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        Response.SetRefreshTokenCookie(result.RefreshToken, env);
        return Ok(result with { RefreshToken = string.Empty });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResultDto>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new LoginCommand(
            request.Email, request.Password, request.BusinessId, HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        Response.SetRefreshTokenCookie(result.RefreshToken, env);
        return Ok(result with { RefreshToken = string.Empty });
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResultDto>> Refresh(CancellationToken ct)
    {
        var refreshToken = Request.GetRefreshTokenCookie();
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new { title = "No refresh token was provided." });
        }

        var result = await mediator.Send(new RefreshTokenCommand(refreshToken, HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

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
            currentUser.UserId!.Value, request.BusinessId, HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        Response.SetRefreshTokenCookie(result.RefreshToken, env);
        return Ok(result with { RefreshToken = string.Empty });
    }
}
