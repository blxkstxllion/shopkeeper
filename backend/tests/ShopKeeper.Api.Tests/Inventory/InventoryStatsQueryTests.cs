namespace ShopKeeper.Api.Tests.Inventory;

using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Inventory.Queries;
using ShopKeeper.Application.Products.Commands;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Infrastructure.Identity;
using ShopKeeper.Infrastructure.Persistence;

public class InventoryStatsQueryTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    [Fact]
    public async Task Handle_ComputesTotalsAcrossActiveProducts()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        await new CreateProductCommandHandler(context, owner).Handle(
            new CreateProductCommand("In stock", "SKU-A", null, null, null, null, 10m, 6m, 5, 10, true, 20, seeded.BranchId),
            CancellationToken.None);
        await new CreateProductCommandHandler(context, owner).Handle(
            new CreateProductCommand("Low stock", "SKU-B", null, null, null, null, 10m, 4m, 5, 25, true, 5, seeded.BranchId),
            CancellationToken.None);
        await new CreateProductCommandHandler(context, owner).Handle(
            new CreateProductCommand("Out of stock", "SKU-C", null, null, null, null, 10m, 2m, 5, 10, true, 0, seeded.BranchId),
            CancellationToken.None);

        var stats = await new GetInventoryStatsQueryHandler(context, owner).Handle(new GetInventoryStatsQuery(null), CancellationToken.None);

        Assert.Equal(3, stats.TotalProducts);
        Assert.Equal(1, stats.LowStockCount);
        Assert.Equal(1, stats.OutOfStockCount);
        Assert.Equal(20 * 6m + 5 * 4m + 0 * 2m, stats.InventoryValue);
    }

    [Fact]
    public async Task Handle_ScopedToOneBranch_ExcludesOtherBranchesStock()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var branchB = new Branch { BusinessId = seeded.BusinessId, Name = "Branch B", Code = "B2", Country = "Ghana" };
        context.Branches.Add(branchB);
        await context.SaveChangesAsync(CancellationToken.None);

        var product = await new CreateProductCommandHandler(context, owner).Handle(
            new CreateProductCommand("Widget", "SKU-BRANCH", null, null, null, null, 10m, 6m, 5, 10, true, 10, seeded.BranchId),
            CancellationToken.None);
        context.ProductStocks.Add(new ProductStock { BusinessId = seeded.BusinessId, ProductId = product.Id, BranchId = branchB.Id, QuantityOnHand = 40 });
        await context.SaveChangesAsync(CancellationToken.None);

        var stats = await new GetInventoryStatsQueryHandler(context, owner).Handle(
            new GetInventoryStatsQuery(seeded.BranchId), CancellationToken.None);

        Assert.Equal(1, stats.TotalProducts);
        Assert.Equal(60m, stats.InventoryValue); // 10 units * 6 cost from branch A only, not branch B's 40
    }

    [Fact]
    public async Task Handle_UserWithoutSalesViewPermission_StillSucceeds()
    {
        // Inventory Manager has inventory:view but not sales:view - this query must not
        // depend on sales:view the way GetDashboardSummaryQuery does.
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        await new CreateProductCommandHandler(context, owner).Handle(
            new CreateProductCommand("Widget", "SKU-IM", null, null, null, null, 10m, 6m, 5, 10, true, 10, seeded.BranchId),
            CancellationToken.None);

        var inventoryManager = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BusinessId = seeded.BusinessId,
            BranchId = seeded.BranchId,
            IsOwner = false,
            PermissionsList = DefaultRoles.RolePermissionKeys[DefaultRoles.InventoryManager].ToList(),
        };

        var stats = await new GetInventoryStatsQueryHandler(context, inventoryManager).Handle(
            new GetInventoryStatsQuery(seeded.BranchId), CancellationToken.None);

        Assert.Equal(1, stats.TotalProducts);
    }

    public void Dispose() => _db.Dispose();
}
