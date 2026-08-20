namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Plans.Commands;

/// <summary>
/// No [Authorize]: this app's authorization model is opt-in per action (plain AddAuthorization(),
/// no default/fallback policy), so omitting it here is already public - not new territory, just
/// this codebase's first action that's intentionally public and secured by a different mechanism
/// (HMAC signature verification) instead of a JWT, since a webhook caller obviously can't present
/// one.
/// </summary>
[ApiController]
[Route("api/webhooks/paystack")]
public class PaystackWebhookController(IPaystackClient paystack, ISender mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        if (!paystack.IsConfigured)
        {
            return NotFound();
        }

        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(ct);

        if (!paystack.VerifyWebhookSignature(rawBody, Request.Headers["x-paystack-signature"]))
        {
            return Unauthorized();
        }

        await mediator.Send(new ProcessPaystackWebhookCommand(rawBody), ct);
        return Ok();
    }
}
