namespace ShopKeeper.Api.Tests.Plans;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Businesses.Commands;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Employees.Commands;
using ShopKeeper.Application.Products.Commands;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Identity;

/// <summary>Free tier's limits (2 branches, 25 products, 2 staff - see PlanLimits) are small
/// enough to hit for real in a test rather than mocking the count, which is what actually proves
/// the check fires at the right count rather than never firing at all.</summary>
public class PlanLimitEnforcementTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    [Fact]
    public async Task CreateBranch_BlockedAtFreeTierLimit()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var handler = new CreateBranchCommandHandler(context, owner, new PlanLimitService(context));

        // Seeding already created "Main Store" (1 of 2 on Free) - one more should still succeed.
        await handler.Handle(new CreateBranchCommand("Second", "B2", null, null, null, null, null), CancellationToken.None);

        // The third branch (now at the 2-branch Free limit) must be blocked.
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new CreateBranchCommand("Third", "B3", null, null, null, null, null), CancellationToken.None));
        Assert.Contains("branch limit", ex.Message);

        Assert.Equal(2, await context.Branches.CountAsync());
    }

    [Fact]
    public async Task CreateProduct_BlockedAtFreeTierLimit()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var handler = new CreateProductCommandHandler(context, owner, new PlanLimitService(context));

        for (var i = 0; i < 25; i++)
        {
            await handler.Handle(
                new CreateProductCommand($"Product {i}", $"SKU-LIM-{i}", null, null, null, null, 10m, 5m, 0, false, 0, null),
                CancellationToken.None);
        }

        var ex = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new CreateProductCommand("One Too Many", "SKU-LIM-25", null, null, null, null, 10m, 5m, 0, false, 0, null),
            CancellationToken.None));
        Assert.Contains("product limit", ex.Message);

        Assert.Equal(25, await context.Products.CountAsync());
    }

    [Fact]
    public async Task CreateProduct_UnlimitedInventoryAddOn_OverridesLimit()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var handler = new CreateProductCommandHandler(context, owner, new PlanLimitService(context));

        var business = await context.Businesses.SingleAsync(b => b.Id == seeded.BusinessId);
        business.HasUnlimitedInventoryAddOn = true;
        await context.SaveChangesAsync(CancellationToken.None);

        for (var i = 0; i < 30; i++) // above the Free tier's 25-product cap
        {
            await handler.Handle(
                new CreateProductCommand($"Product {i}", $"SKU-ADDON-{i}", null, null, null, null, 10m, 5m, 0, false, 0, null),
                CancellationToken.None);
        }

        Assert.Equal(30, await context.Products.CountAsync());
    }

    [Fact]
    public async Task ApproveJoinRequest_BlockedAtFreeTierStaffLimit()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var code = await new RegenerateJoinCodeCommandHandler(context, owner).Handle(new RegenerateJoinCodeCommand(), CancellationToken.None);
        var cashierRole = await context.Roles.SingleAsync(r => r.Name == DefaultRoles.Cashier);

        // Seeding already created the Owner (1 of 2 staff on Free) - one more should still succeed.
        await new SubmitJoinRequestCommandHandler(context, _hasher, new NotificationDispatcher(context)).Handle(
            new SubmitJoinRequestCommand(code, "Kofi", "Mensah", "kofi-limit-1@shop.test", "0244000010", "Passw0rd!"),
            CancellationToken.None);
        var firstJoinRequest = await context.JoinRequests.SingleAsync(r => r.User.Email == "kofi-limit-1@shop.test");
        await new ApproveJoinRequestCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new ApproveJoinRequestCommand(firstJoinRequest.Id, cashierRole.Id, seeded.BranchId), CancellationToken.None);

        // The third staff member (now at the 2-staff Free limit) must be blocked.
        await new SubmitJoinRequestCommandHandler(context, _hasher, new NotificationDispatcher(context)).Handle(
            new SubmitJoinRequestCommand(code, "Ama", "Boateng", "ama-limit-2@shop.test", "0244000011", "Passw0rd!"),
            CancellationToken.None);
        var secondJoinRequest = await context.JoinRequests.SingleAsync(r => r.User.Email == "ama-limit-2@shop.test");

        var ex = await Assert.ThrowsAsync<ConflictException>(() => new ApproveJoinRequestCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new ApproveJoinRequestCommand(secondJoinRequest.Id, cashierRole.Id, seeded.BranchId), CancellationToken.None));
        Assert.Contains("staff limit", ex.Message);

        Assert.Equal(2, await context.BusinessUsers.CountAsync(bu => bu.Status != BusinessUserStatus.Removed));
    }

    public void Dispose() => _db.Dispose();
}
