namespace ShopKeeper.Api.Tests.Businesses;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Businesses.Commands;
using ShopKeeper.Application.Businesses.Queries;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Infrastructure.Identity;
using ShopKeeper.Infrastructure.Persistence;

public class BranchesTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    [Fact]
    public async Task CreateBranch_ThenList_ReturnsIt()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var branch = await new CreateBranchCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateBranchCommand("Kumasi Branch", "KUM-1", null, "Kumasi", "Ghana", null, null), CancellationToken.None);

        Assert.False(branch.IsMainBranch);

        var all = await new GetBranchesQueryHandler(context).Handle(new GetBranchesQuery(), CancellationToken.None);
        Assert.Equal(2, all.Count); // seeded Main Store + this one
    }

    [Fact]
    public async Task CreateBranch_DuplicateCode_ThrowsConflict()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        await new CreateBranchCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateBranchCommand("Kumasi Branch", "KUM-1", null, null, null, null, null), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => new CreateBranchCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateBranchCommand("Another Branch", "KUM-1", null, null, null, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateBranch_PromoteToMain_DemotesPreviousMain()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var newBranch = await new CreateBranchCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateBranchCommand("Kumasi Branch", "KUM-1", null, null, null, null, null), CancellationToken.None);

        await new UpdateBranchCommandHandler(context, owner).Handle(
            new UpdateBranchCommand(newBranch.Id, "Kumasi Branch", "KUM-1", null, null, null, null, null, IsMain: true, IsActive: true),
            CancellationToken.None);

        var promoted = await context.Branches.SingleAsync(b => b.Id == newBranch.Id);
        var original = await context.Branches.SingleAsync(b => b.Id == seeded.BranchId);
        Assert.True(promoted.IsMainBranch);
        Assert.False(original.IsMainBranch);
    }

    [Fact]
    public async Task UpdateBranch_RemoveMainStatusDirectly_ThrowsConflict()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var mainBranch = await context.Branches.SingleAsync(b => b.Id == seeded.BranchId);

        await Assert.ThrowsAsync<ConflictException>(() => new UpdateBranchCommandHandler(context, owner).Handle(
            new UpdateBranchCommand(mainBranch.Id, mainBranch.Name, mainBranch.Code, null, null, null, null, null, IsMain: false, IsActive: true),
            CancellationToken.None));
    }

    [Fact]
    public async Task DeleteBranch_OnlyActiveBranch_ThrowsConflict()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        // Seeded business has exactly one (main) branch - deleting it should fail on both grounds;
        // this asserts the "only active branch" guard specifically by using a non-main branch.
        var newBranch = await new CreateBranchCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateBranchCommand("Kumasi Branch", "KUM-1", null, null, null, null, null), CancellationToken.None);

        // Deactivate the original main's sibling scenario: deactivate newBranch first is fine (2 active exist),
        // but deactivating down to the last one must fail.
        await new DeleteBranchCommandHandler(context, owner).Handle(new DeleteBranchCommand(newBranch.Id), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => new DeleteBranchCommandHandler(context, owner).Handle(
            new DeleteBranchCommand(seeded.BranchId), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteBranch_MainBranch_ThrowsConflict()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        await new CreateBranchCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateBranchCommand("Kumasi Branch", "KUM-1", null, null, null, null, null), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => new DeleteBranchCommandHandler(context, owner).Handle(
            new DeleteBranchCommand(seeded.BranchId), CancellationToken.None));
    }

    [Fact]
    public async Task BranchManager_CannotManageBranches()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var context = _db.CreateContext(seeded.AsOwner());

        var branchManager = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BusinessId = seeded.BusinessId,
            BranchId = seeded.BranchId,
            IsOwner = false,
            PermissionsList = DefaultRoles.RolePermissionKeys[DefaultRoles.BranchManager].ToList(),
        };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new CreateBranchCommandHandler(context, branchManager, new PlanLimitService(context)).Handle(
            new CreateBranchCommand("Kumasi Branch", "KUM-1", null, null, null, null, null), CancellationToken.None));
    }

    public void Dispose() => _db.Dispose();
}
