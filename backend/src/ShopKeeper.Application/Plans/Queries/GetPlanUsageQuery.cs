namespace ShopKeeper.Application.Plans.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Plans.Dtos;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Enums;

public record GetPlanUsageQuery : IRequest<PlanUsageDto>;

/// <summary>Viewing plan/usage is not itself a paid feature (unlike Reports/the AI Advisor) -
/// any business member can see it, so the Settings page can render it read-only for non-owners.
/// Only changing the plan (SetPlanTierCommand) is owner-only.</summary>
public class GetPlanUsageQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetPlanUsageQuery, PlanUsageDto>
{
    public async Task<PlanUsageDto> Handle(GetPlanUsageQuery request, CancellationToken cancellationToken)
    {
        var businessId = currentUser.RequireBusinessId();

        var business = await db.Businesses.FirstAsync(b => b.Id == businessId, cancellationToken);
        var limits = PlanLimits.For(business.PlanTier);

        var branchCount = await db.Branches.CountAsync(b => b.BusinessId == businessId, cancellationToken);
        var productCount = await db.Products.CountAsync(p => p.BusinessId == businessId && p.IsActive, cancellationToken);
        var staffCount = await db.BusinessUsers.CountAsync(
            bu => bu.BusinessId == businessId && bu.Status != BusinessUserStatus.Removed, cancellationToken);

        return new PlanUsageDto(business.PlanTier, limits, branchCount, productCount, staffCount, business.HasUnlimitedInventoryAddOn);
    }
}
