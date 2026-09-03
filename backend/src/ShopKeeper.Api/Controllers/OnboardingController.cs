namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Api.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Onboarding.Commands;
using ShopKeeper.Application.Onboarding.Dtos;
using ShopKeeper.Domain.Enums;

[Authorize]
[ApiController]
[Route("api/onboarding")]
public class OnboardingController(ISender mediator, ICurrentUserService currentUser, IWebHostEnvironment env) : ControllerBase
{
    public record CompleteOnboardingRequest(
        string BusinessName,
        BusinessType BusinessType,
        string? BusinessTypeOther,
        string Country,
        string CurrencyCode,
        string? LogoUrl,
        bool TaxEnabled,
        decimal TaxRatePercent,
        bool TaxInclusivePricing,
        IReadOnlyList<BusinessGoal> Goals,
        string FirstBranchName,
        string? FirstBranchAddress,
        string? FirstBranchCity,
        string ColorTheme = "green");

    [HttpPost("complete")]
    public async Task<ActionResult<BusinessDto>> Complete(CompleteOnboardingRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CompleteOnboardingCommand(
            currentUser.UserId!.Value,
            request.BusinessName,
            request.BusinessType,
            request.BusinessTypeOther,
            request.Country,
            request.CurrencyCode,
            request.LogoUrl,
            request.TaxEnabled,
            request.TaxRatePercent,
            request.TaxInclusivePricing,
            request.Goals,
            request.FirstBranchName,
            request.FirstBranchAddress,
            request.FirstBranchCity,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            request.ColorTheme), ct);

        // Always session-only here - onboarding completion issues a fresh session right after
        // registration, which is already session-only (see RegisterCommand's rememberMe: false).
        Response.SetRefreshTokenCookie(result.RefreshToken, env, persistent: false);
        return Ok(result with { RefreshToken = string.Empty });
    }
}
