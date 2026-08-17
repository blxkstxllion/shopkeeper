namespace ShopKeeper.Application.Plans.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;

public record SetInventoryAddOnCommand(bool Enabled) : IRequest;

/// <summary>Same self-serve, owner-only, no-real-charge-yet shape as SetPlanTierCommand - see its
/// doc comment. Overrides the current plan's product cap entirely while enabled (see
/// PlanLimitService.EnsureCanAddProductAsync), independent of which tier the business is on.</summary>
public class SetInventoryAddOnCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<SetInventoryAddOnCommand>
{
    public async Task Handle(SetInventoryAddOnCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsOwner)
        {
            throw new ForbiddenAccessException("Only the business owner can change the plan.");
        }

        var businessId = currentUser.RequireBusinessId();
        var business = await db.Businesses.FirstAsync(b => b.Id == businessId, cancellationToken);

        business.HasUnlimitedInventoryAddOn = request.Enabled;

        await db.SaveChangesAsync(cancellationToken);
    }
}
