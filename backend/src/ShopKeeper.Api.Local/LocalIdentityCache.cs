namespace ShopKeeper.Api.Local;

using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Interfaces;

/// <summary>Concrete ILocalIdentityCache - lives here (not Infrastructure) since it's wired up only
/// by this edition's Program.cs, same as LocalAuthenticationHandler. Registered as a singleton and
/// refreshed in two places: once at startup (Program.cs, so a restart of an already-set-up install
/// is authenticated from the first request) and again right after CompleteLocalSetupCommand runs
/// (so the very next request after first-run setup is authenticated with no process restart).</summary>
public class LocalIdentityCache(IServiceScopeFactory scopeFactory) : ILocalIdentityCache
{
    public LocalIdentitySnapshot Current { get; private set; } = LocalIdentitySnapshot.Empty;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        // IgnoreQueryFilters: there's no authenticated identity yet to satisfy AppDbContext's
        // tenant filter at the moment this runs (that's the whole point of this cache) - same
        // cross-tenant-but-trusted rationale as TokenIssuer resolving a user's own memberships,
        // see AppDbContext's doc comment. Safe here specifically because this is a single-tenant
        // local database with at most one BusinessUser row ever.
        var businessUser = await db.BusinessUsers.IgnoreQueryFilters().FirstOrDefaultAsync(cancellationToken);
        if (businessUser is null)
        {
            Current = LocalIdentitySnapshot.Empty;
            return;
        }

        var mainBranch = await db.Branches.IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.BusinessId == businessUser.BusinessId && b.IsMainBranch, cancellationToken);

        Current = new LocalIdentitySnapshot(businessUser.UserId, businessUser.BusinessId, mainBranch?.Id, businessUser.IsOwner);
    }
}
