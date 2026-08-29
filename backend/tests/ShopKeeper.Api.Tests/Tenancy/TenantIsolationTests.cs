namespace ShopKeeper.Api.Tests.Tenancy;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Auth.Commands;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Onboarding.Commands;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Identity;
using ShopKeeper.Infrastructure.Persistence;

/// <summary>
/// Proves AppDbContext's global query filters actually isolate tenants at runtime - not just
/// that the filter expression compiles. This exercises the exact scenario that would break if
/// EF Core's cached, process-wide compiled model ever ends up binding a query filter to the
/// wrong request's ICurrentUserService (see the comment on AppDbContext.TenantBusinessId).
/// </summary>
public class TenantIsolationTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(new JwtSettings
    {
        Issuer = "ShopKeeper.Tests",
        Audience = "ShopKeeper.Tests",
        Secret = "test-secret-at-least-32-bytes-long-for-hmac-sha256",
    }));

    private async Task<(Guid BusinessId, Guid BranchId, Guid OwnerId)> CreateOnboardedBusiness(string ownerEmail, string businessName)
    {
        var setupUser = new TestCurrentUserService();
        var setupContext = _db.CreateContext(setupUser);
        var tokenIssuer = new TokenIssuer(setupContext, _jwt);

        var registerResult = await new RegisterCommandHandler(setupContext, _hasher, tokenIssuer, new TestEmailSender()).Handle(
            new RegisterCommand(ownerEmail, "Passw0rd!", "Owner", businessName, null), CancellationToken.None);

        var business = await new CompleteOnboardingCommandHandler(setupContext, tokenIssuer).Handle(
            new CompleteOnboardingCommand(
                registerResult.User.Id, businessName, BusinessType.Retail, null, "Ghana", "GHS", null,
                false, 0, true, [], "Main", null, null, null),
            CancellationToken.None);

        return (business.Id, business.FirstBranchId, registerResult.User.Id);
    }

    [Fact]
    public async Task Branches_QueriedFromBusinessA_NeverIncludeBusinessBsBranch()
    {
        var (businessAId, _, _) = await CreateOnboardedBusiness("owner-a@shop.test", "Shop A");
        var (businessBId, _, _) = await CreateOnboardedBusiness("owner-b@shop.test", "Shop B");

        var viewerAsA = new TestCurrentUserService { BusinessId = businessAId };
        var contextAsA = _db.CreateContext(viewerAsA);

        var visibleBranches = await contextAsA.Branches.ToListAsync();

        var visibleBusinessIds = await contextAsA.Branches.IgnoreQueryFilters()
            .Where(b => visibleBranches.Select(v => v.Id).Contains(b.Id))
            .Select(b => b.BusinessId)
            .Distinct()
            .ToListAsync();

        Assert.Single(visibleBranches);
        Assert.Equal([businessAId], visibleBusinessIds);
        Assert.DoesNotContain(visibleBranches, b => b.BusinessId == businessBId);
    }

    [Fact]
    public async Task SwitchingCurrentUserBusinessId_OnANewContext_ChangesVisibleData()
    {
        var (businessAId, _, _) = await CreateOnboardedBusiness("switch-a@shop.test", "Switch Shop A");
        var (businessBId, _, _) = await CreateOnboardedBusiness("switch-b@shop.test", "Switch Shop B");

        var viewerAsA = new TestCurrentUserService { BusinessId = businessAId };
        var rolesForA = await _db.CreateContext(viewerAsA).Roles.Select(r => r.BusinessId).Distinct().ToListAsync();

        var viewerAsB = new TestCurrentUserService { BusinessId = businessBId };
        var rolesForB = await _db.CreateContext(viewerAsB).Roles.Select(r => r.BusinessId).Distinct().ToListAsync();

        Assert.Equal([businessAId], rolesForA);
        Assert.Equal([businessBId], rolesForB);
    }

    [Fact]
    public async Task BusinessUsers_QueriedWithoutAnActiveBusiness_ReturnsNoRows()
    {
        await CreateOnboardedBusiness("noctx-a@shop.test", "No Context Shop A");

        var viewerWithNoBusiness = new TestCurrentUserService { BusinessId = null };
        var context = _db.CreateContext(viewerWithNoBusiness);

        var visible = await context.BusinessUsers.ToListAsync();

        Assert.Empty(visible);
    }

    [Fact]
    public async Task DataCreatedAfterAnotherTenantsContextBuiltTheModel_IsStillCorrectlyIsolated()
    {
        // Regression guard: build (and query through) Business A's context FIRST, so if the
        // compiled model's filter ever closed over that first ICurrentUserService instance
        // instead of re-reading it per-instance, this second business would incorrectly see
        // (or be filtered by) stale state from the first.
        var (businessAId, _, _) = await CreateOnboardedBusiness("first-a@shop.test", "First Shop A");
        var earlyViewerAsA = new TestCurrentUserService { BusinessId = businessAId };
        _ = await _db.CreateContext(earlyViewerAsA).Branches.ToListAsync();

        var (businessBId, _, _) = await CreateOnboardedBusiness("second-b@shop.test", "Second Shop B");

        var viewerAsB = new TestCurrentUserService { BusinessId = businessBId };
        var branchesForB = await _db.CreateContext(viewerAsB).Branches.ToListAsync();

        Assert.Single(branchesForB);
        Assert.All(branchesForB, b => Assert.Equal(businessBId, b.BusinessId));
    }

    public void Dispose() => _db.Dispose();
}
