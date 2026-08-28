namespace ShopKeeper.Application.Plans.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Plans;
using ShopKeeper.Application.Plans.Dtos;

public record VerifyCheckoutCommand(string Reference) : IRequest<VerifyCheckoutResultDto>;

/// <summary>
/// The synchronous, critical-path activation of a purchase - called when the user's browser
/// redirects back from Paystack's hosted checkout page. Deliberately independent of webhook
/// delivery (see ProcessPaystackWebhookCommand): a purchase must not depend on a webhook ever
/// firing to take effect, only on this call succeeding. Idempotent by construction - safe to call
/// twice (e.g. a page refresh on the callback page), since every step is an upsert of the same
/// values.
/// </summary>
public class VerifyCheckoutCommandHandler(IAppDbContext db, ICurrentUserService currentUser, IPaystackClient paystack)
    : IRequestHandler<VerifyCheckoutCommand, VerifyCheckoutResultDto>
{
    public async Task<VerifyCheckoutResultDto> Handle(VerifyCheckoutCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsOwner)
        {
            throw new ForbiddenAccessException("Only the business owner can change the plan.");
        }

        if (!CheckoutReference.TryParse(request.Reference, out var referenceBusinessId, out var tier))
        {
            throw new ConflictException("Malformed checkout reference.");
        }

        if (referenceBusinessId != currentUser.RequireBusinessId())
        {
            throw new ForbiddenAccessException("Reference does not belong to your business.");
        }

        var result = await paystack.VerifyTransactionAsync(request.Reference, cancellationToken);
        if (!result.Success)
        {
            return new VerifyCheckoutResultDto(false, null);
        }

        var business = await db.Businesses.FirstAsync(b => b.Id == referenceBusinessId, cancellationToken);
        business.PaystackCustomerCode = result.CustomerCode;

        var subscription = await paystack.FindActiveSubscriptionAsync(result.CustomerCode, tier, cancellationToken);
        if (subscription is not null)
        {
            business.PaystackSubscriptionCode = subscription.SubscriptionCode;
            business.PaystackSubscriptionEmailToken = subscription.EmailToken;
            business.PaystackSubscriptionStatus = subscription.Status;
            business.PaystackCurrentPeriodEnd = subscription.NextPaymentDate;
        }

        business.PlanTier = tier;
        await db.SaveChangesAsync(cancellationToken);

        return new VerifyCheckoutResultDto(true, tier);
    }
}
