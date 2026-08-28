namespace ShopKeeper.Application.Common.Services;

using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Enums;

/// <summary>
/// Enforces the branch/product/staff caps in PlanLimits at the three kinds of places new
/// resources actually get created. Downgrading a plan never deletes or deactivates anything
/// already over the new limit - these checks only ever block *new* creation, so a plan change
/// has no destructive side effect on existing data.
///
/// Every count query here uses IgnoreQueryFilters() with an explicit BusinessId predicate - not
/// because the normal tenant filter is wrong, but because businessId is always an explicitly
/// validated parameter, not necessarily the caller's own ambient tenant context. Two of the three
/// call sites (AcceptInvitationCommand, AcceptInvitationForExistingUserCommand) accept an
/// invitation into a business that isn't - or, for the unauthenticated case, can't be - the
/// caller's active tenant, exactly like those handlers' own existing IgnoreQueryFilters() lookups
/// on PendingInvitation/Roles. Relying on the implicit filter here would silently under-count (or
/// zero out) staff in precisely that case.
/// </summary>
public class PlanLimitService(IAppDbContext db)
{
    public async Task EnsureCanAddBranchAsync(Guid businessId, CancellationToken ct)
    {
        var limits = await GetLimitsAsync(businessId, ct);
        var count = await db.Branches.IgnoreQueryFilters().CountAsync(b => b.BusinessId == businessId, ct);
        if (count >= limits.MaxBranches)
        {
            throw new ConflictException("You've reached your plan's branch limit. Upgrade to add more.");
        }
    }

    public async Task EnsureCanAddProductAsync(Guid businessId, CancellationToken ct)
    {
        var business = await db.Businesses.FirstAsync(b => b.Id == businessId, ct);
        if (business.HasUnlimitedInventoryAddOn)
        {
            return;
        }

        var limits = PlanLimits.For(business.PlanTier);
        var count = await db.Products.IgnoreQueryFilters().CountAsync(p => p.BusinessId == businessId && p.IsActive, ct);
        if (count >= limits.MaxProducts)
        {
            throw new ConflictException("You've reached your plan's product limit. Upgrade to add more.");
        }
    }

    public async Task EnsureCanAddStaffAsync(Guid businessId, CancellationToken ct)
    {
        var limits = await GetLimitsAsync(businessId, ct);
        var count = await db.BusinessUsers.IgnoreQueryFilters()
            .CountAsync(bu => bu.BusinessId == businessId && bu.Status != BusinessUserStatus.Removed, ct);
        if (count >= limits.MaxStaff)
        {
            throw new ConflictException("You've reached your plan's staff limit. Upgrade to add more.");
        }
    }

    private async Task<PlanLimitInfo> GetLimitsAsync(Guid businessId, CancellationToken ct)
    {
        var tier = await db.Businesses.Where(b => b.Id == businessId).Select(b => b.PlanTier).FirstAsync(ct);
        return PlanLimits.For(tier);
    }
}
