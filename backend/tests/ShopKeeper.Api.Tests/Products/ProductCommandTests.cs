namespace ShopKeeper.Api.Tests.Products;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Products.Commands;
using ShopKeeper.Infrastructure.Identity;

public class ProductCommandTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    [Fact]
    public async Task CreateProduct_WithInitialQuantity_CreatesProductStockAndInventoryTransaction()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var handler = new CreateProductCommandHandler(context, owner, new PlanLimitService(context));
        var result = await handler.Handle(
            new CreateProductCommand("Coca-Cola 500ml", "SKU-001", "5449000000996", null, null, null, 5.00m, 3.00m, 20, true, 50, seeded.BranchId),
            CancellationToken.None);

        Assert.Equal(50, result.QuantityOnHand);
        Assert.False(result.IsLowStock);

        var stock = await context.ProductStocks.SingleAsync(s => s.ProductId == result.Id);
        Assert.Equal(50, stock.QuantityOnHand);

        var transaction = await context.InventoryTransactions.SingleAsync(t => t.ProductId == result.Id);
        Assert.Equal(Domain.Enums.InventoryTransactionType.InitialStock, transaction.Type);
        Assert.Equal(50, transaction.QuantityChange);
        Assert.Equal(50, transaction.QuantityAfter);
    }

    [Fact]
    public async Task CreateProduct_WithoutInitialQuantity_CreatesNoInventoryTransaction()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Service Fee", "SKU-SVC", null, null, null, null, 10m, 0m, 0, false, 0, null),
            CancellationToken.None);

        Assert.Empty(context.InventoryTransactions);
    }

    [Fact]
    public async Task CreateProduct_WithDuplicateSku_ThrowsConflict()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var handler = new CreateProductCommandHandler(context, owner, new PlanLimitService(context));

        await handler.Handle(
            new CreateProductCommand("Product A", "DUP-SKU", null, null, null, null, 5m, 3m, 0, false, 0, null),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new CreateProductCommand("Product B", "DUP-SKU", null, null, null, null, 8m, 4m, 0, false, 0, null),
            CancellationToken.None));
    }

    [Fact]
    public async Task CreateProduct_WithoutProductsManagePermission_ThrowsForbidden()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var cashier = new TestCurrentUserService { UserId = seeded.OwnerId, BusinessId = seeded.BusinessId, IsOwner = false };
        var context = _db.CreateContext(cashier);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new CreateProductCommandHandler(context, cashier, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Product A", "SKU-X", null, null, null, null, 5m, 3m, 0, false, 0, null),
            CancellationToken.None));
    }

    [Fact]
    public async Task DeleteProduct_SoftDeletes_ProductRemainsInDatabase()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Product A", "SKU-DEL", null, null, null, null, 5m, 3m, 0, false, 0, null),
            CancellationToken.None);

        await new DeleteProductCommandHandler(context, owner).Handle(new DeleteProductCommand(product.Id), CancellationToken.None);

        var stored = await context.Products.SingleAsync(p => p.Id == product.Id);
        Assert.False(stored.IsActive);
    }

    public void Dispose() => _db.Dispose();
}
