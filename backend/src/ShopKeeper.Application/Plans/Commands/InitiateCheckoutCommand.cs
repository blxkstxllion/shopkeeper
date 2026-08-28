namespace ShopKeeper.Application.Plans.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Plans;
using ShopKeeper.Application.Plans.Dtos;
using ShopKeeper.Domain.Enums;

public record InitiateCheckoutCommand(PlanTier RequestedTier) : IRequest<CheckoutSessionDto>;

/// <summary>
/// Starts a Paystack subscription checkout for one of the 4 paid tiers. Returns a hosted
/// checkout URL for the frontend to redirect to - nothing about the business changes here,
/// since the subscription isn't real until VerifyCheckoutCommand confirms payment succeeded.
/// </summary>
public class InitiateCheckoutCommandHandler(IAppDbContext db, ICurrentUserService currentUser, IPaystackClient paystack)
    : IRequestHandler<InitiateCheckoutCommand, CheckoutSessionDto>
{
    public async Task<CheckoutSessionDto> Handle(InitiateCheckoutCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsOwner)
        {
            throw new ForbiddenAccessException("Only the business owner can change the plan.");
        }

        if (request.RequestedTier == PlanTier.Free)
        {
            throw new ConflictException("Free doesn't require checkout - use the plan-change endpoint instead.");
        }

        if (!paystack.IsConfigured)
        {
            throw new ConflictException("Billing isn't configured for this environment.");
        }

        var businessId = currentUser.RequireBusinessId();
        var userId = currentUser.RequireUserId();
        var owner = await db.Users.FirstAsync(u => u.Id == userId, cancellationToken);

        var reference = CheckoutReference.Build(businessId, request.RequestedTier);

        var session = await paystack.InitializeSubscriptionCheckoutAsync(
            owner.Email, request.RequestedTier, reference, cancellationToken);

        return new CheckoutSessionDto(session.AuthorizationUrl);
    }
}
