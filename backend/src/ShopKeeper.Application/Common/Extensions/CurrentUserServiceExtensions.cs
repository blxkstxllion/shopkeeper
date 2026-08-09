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
}
