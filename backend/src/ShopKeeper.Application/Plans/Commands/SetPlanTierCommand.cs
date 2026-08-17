namespace ShopKeeper.Application.Plans.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Enums;

public record SetPlanTierCommand(PlanTier NewTier) : IRequest;

/// <summary>
/// Self-serve, not payment-backed - there's no real charge here yet (see Business.PlanTier).
/// Owner-only rather than gated by a permission key, matching how this codebase already treats
/// ownership as a first-class concept for other billing-adjacent/high-stakes actions (e.g. only
/// an owner may remove another owner). Switching tiers never touches existing branches/products/
/// staff, even when the new tier's limits are lower than current usage - downgrading only blocks
/// *new* creation going forward (see PlanLimitService), it's never destructive.
/// </summary>
public class SetPlanTierCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<SetPlanTierCommand>
{
    public async Task Handle(SetPlanTierCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsOwner)
        {
            throw new ForbiddenAccessException("Only the business owner can change the plan.");
        }

        var businessId = currentUser.RequireBusinessId();
        var business = await db.Businesses.FirstAsync(b => b.Id == businessId, cancellationToken);

        business.PlanTier = request.NewTier;

        await db.SaveChangesAsync(cancellationToken);
    }
}
