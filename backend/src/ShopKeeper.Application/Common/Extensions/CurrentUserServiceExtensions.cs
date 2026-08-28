namespace ShopKeeper.Application.Common.Extensions;

using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;

public static class CurrentUserServiceExtensions
{
    /// <summary>The active tenant, or a 403 if the caller hasn't selected/joined a business - every
    /// Phase 2+ command needs this, so handlers shouldn't each re-derive the same failure mode.</summary>
    public static Guid RequireBusinessId(this ICurrentUserService currentUser) =>
        currentUser.BusinessId ?? throw new ForbiddenAccessException("No active business selected.");

    public static Guid RequireUserId(this ICurrentUserService currentUser) =>
        currentUser.UserId ?? throw new AuthenticationException("Not authenticated.");

    public static void RequirePermission(this ICurrentUserService currentUser, string permissionKey)
    {
        if (!currentUser.HasPermission(permissionKey))
        {
            throw new ForbiddenAccessException($"This action requires the '{permissionKey}' permission.");
        }
    }

    /// <summary>
    /// Users with a fixed BranchId (Cashier, Branch Manager, ...) may only act on their own
    /// branch. Users with no BranchId (Owner, Administrator - unrestricted roles) can act on
    /// any branch in the business. Without this, a Cashier could pass an arbitrary BranchId in
    /// a request body and ring up sales or adjust stock at a branch they're not assigned to.
    /// </summary>
    public static void RequireBranchAccess(this ICurrentUserService currentUser, Guid branchId)
    {
        if (currentUser.BranchId.HasValue && currentUser.BranchId.Value != branchId)
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }
    }
}
