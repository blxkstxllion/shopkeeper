namespace ShopKeeper.Application.Common.Interfaces;

/// <summary>Offline-edition-only. A snapshot of the single local user's identity, read once
/// from the DB and cached - see LocalAuthenticationHandler (ShopKeeper.Api.Local), which stamps
/// these values as claims on every request so CurrentUserService/AppDbContext's tenant filter
/// work completely unchanged from the SaaS build. IsOwner=true alone is enough for every
/// permission check to pass (see ICurrentUserService.HasPermission), so there's no need to
/// track individual permission keys for a single-owner local setup.</summary>
public record LocalIdentitySnapshot(Guid? UserId, Guid? BusinessId, Guid? BranchId, bool IsOwner)
{
    public static readonly LocalIdentitySnapshot Empty = new(null, null, null, false);
}

/// <summary>Refreshed by CompleteLocalSetupCommand right after setup creates the Business/User/
/// BusinessUser graph, so the very next request is correctly authenticated with no process
/// restart needed.</summary>
public interface ILocalIdentityCache
{
    LocalIdentitySnapshot Current { get; }

    Task RefreshAsync(CancellationToken cancellationToken = default);
}
