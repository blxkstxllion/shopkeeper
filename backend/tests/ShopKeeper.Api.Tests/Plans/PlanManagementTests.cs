namespace ShopKeeper.Api.Tests.Plans;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Businesses.Commands;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Plans.Commands;
using ShopKeeper.Application.Plans.Queries;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Identity;

public class PlanManagementTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    [Fact]
    public async Task GetPlanUsage_NewBusiness_IsFreeTierWithCorrectCounts()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var usage = await new GetPlanUsageQueryHandler(context, owner).Handle(new GetPlanUsageQuery(), CancellationToken.None);

        Assert.Equal(PlanTier.Free, usage.CurrentTier);
        Assert.Equal(2, usage.Limits.MaxBranches);
        Assert.Equal(1, usage.BranchCount); // "Main Store" from onboarding
        Assert.Equal(0, usage.ProductCount);
        Assert.Equal(1, usage.StaffCount); // the owner
        Assert.False(usage.HasUnlimitedInventoryAddOn);
    }

    [Fact]
    public async Task SetPlanTier_Owner_ChangesTierImmediately_NoPaymentInvolved()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        await new SetPlanTierCommandHandler(context, owner).Handle(new SetPlanTierCommand(PlanTier.EnterpriseAi), CancellationToken.None);

        var business = await context.Businesses.SingleAsync(b => b.Id == seeded.BusinessId);
        Assert.Equal(PlanTier.EnterpriseAi, business.PlanTier);
    }

    [Fact]
    public async Task SetPlanTier_NonOwner_ThrowsForbidden()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var context = _db.CreateContext(seeded.AsOwner());

        var nonOwner = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BusinessId = seeded.BusinessId,
            BranchId = seeded.BranchId,
            IsOwner = false,
            PermissionsList = ["settings:manage"], // even with settings:manage, plan changes are owner-only
        };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new SetPlanTierCommandHandler(context, nonOwner).Handle(new SetPlanTierCommand(PlanTier.Enterprise), CancellationToken.None));

        var business = await context.Businesses.SingleAsync(b => b.Id == seeded.BusinessId);
        Assert.Equal(PlanTier.Free, business.PlanTier); // unchanged
    }

    [Fact]
    public async Task SetInventoryAddOn_Owner_Toggles()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        await new SetInventoryAddOnCommandHandler(context, owner).Handle(new SetInventoryAddOnCommand(true), CancellationToken.None);
        Assert.True((await context.Businesses.SingleAsync(b => b.Id == seeded.BusinessId)).HasUnlimitedInventoryAddOn);

        await new SetInventoryAddOnCommandHandler(context, owner).Handle(new SetInventoryAddOnCommand(false), CancellationToken.None);
        Assert.False((await context.Businesses.SingleAsync(b => b.Id == seeded.BusinessId)).HasUnlimitedInventoryAddOn);
    }

    [Fact]
    public async Task Downgrade_NeverTouchesExistingOverLimitResources()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        // Start on Enterprise (unlimited branches) and create well above Business's 5-branch cap.
        await new SetPlanTierCommandHandler(context, owner).Handle(new SetPlanTierCommand(PlanTier.Enterprise), CancellationToken.None);
        var branchHandler = new CreateBranchCommandHandler(context, owner, new PlanLimitService(context));
        for (var i = 0; i < 7; i++) // + the 1 seeded branch = 8 total, well above 5
        {
            await branchHandler.Handle(new CreateBranchCommand($"Branch {i}", $"DG{i}", null, null, null, null, null), CancellationToken.None);
        }
        Assert.Equal(8, await context.Branches.CountAsync());

        // Downgrade to Business (5-branch cap) - nothing existing should be deleted or deactivated.
        await new SetPlanTierCommandHandler(context, owner).Handle(new SetPlanTierCommand(PlanTier.Business), CancellationToken.None);
        Assert.Equal(8, await context.Branches.CountAsync());

        // But creating a 9th is now blocked, since usage is already over the new limit.
        await Assert.ThrowsAsync<ConflictException>(() =>
            branchHandler.Handle(new CreateBranchCommand("One More", "DG-X", null, null, null, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task PlanChanges_NeverAffectAnotherBusiness()
    {
        var businessA = await PosTestFixture.SeedAsync(_db, _hasher, _jwt, "owner-a@shop.test");
        var businessB = await PosTestFixture.SeedAsync(_db, _hasher, _jwt, "owner-b@shop.test");
        var ownerA = businessA.AsOwner();
        var ownerB = businessB.AsOwner();

        await new SetPlanTierCommandHandler(_db.CreateContext(ownerA), ownerA).Handle(
            new SetPlanTierCommand(PlanTier.EnterpriseAi), CancellationToken.None);

        var contextB = _db.CreateContext(ownerB);
        var businessBEntity = await contextB.Businesses.SingleAsync(b => b.Id == businessB.BusinessId);
        Assert.Equal(PlanTier.Free, businessBEntity.PlanTier); // untouched by Business A's change

        var usageB = await new GetPlanUsageQueryHandler(contextB, ownerB).Handle(new GetPlanUsageQuery(), CancellationToken.None);
        Assert.Equal(PlanTier.Free, usageB.CurrentTier);
    }

    public void Dispose() => _db.Dispose();
}
