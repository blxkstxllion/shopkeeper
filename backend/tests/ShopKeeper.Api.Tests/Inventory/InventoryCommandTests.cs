namespace ShopKeeper.Api.Tests.Inventory;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Inventory.Commands;
using ShopKeeper.Application.Products.Commands;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Identity;
using ShopKeeper.Infrastructure.Persistence;

public class InventoryCommandTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    private async Task<(PosTestFixture.SeededBusiness Seeded, AppDbContext Context, TestCurrentUserService Owner, Guid ProductId)> SeedWithProductAsync(int initialQuantity = 20)
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-INV", null, null, null, null, 5m, 3m, 5, 10, true, initialQuantity, seeded.BranchId),
            CancellationToken.None);

        return (seeded, context, owner, product.Id);
    }

    [Fact]
    public async Task AdjustStock_PositiveChange_IncreasesQuantityAndRecordsTransaction()
    {
        var (seeded, context, owner, productId) = await SeedWithProductAsync(initialQuantity: 20);

        var newQuantity = await new AdjustStockCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new AdjustStockCommand(productId, seeded.BranchId, 15, "Received delivery"), CancellationToken.None);

        Assert.Equal(35, newQuantity);
        var stock = await context.ProductStocks.SingleAsync(s => s.ProductId == productId);
        Assert.Equal(35, stock.QuantityOnHand);
    }

    [Fact]
    public async Task AdjustStock_NegativeChangeBelowZero_ThrowsConflict()
    {
        var (seeded, context, owner, productId) = await SeedWithProductAsync(initialQuantity: 5);

        await Assert.ThrowsAsync<ConflictException>(() => new AdjustStockCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new AdjustStockCommand(productId, seeded.BranchId, -10, "Stock count correction"), CancellationToken.None));

        var stock = await context.ProductStocks.SingleAsync(s => s.ProductId == productId);
        Assert.Equal(5, stock.QuantityOnHand); // unchanged - the rejected adjustment must not partially apply
    }

    [Fact]
    public async Task AdjustStock_RecordsInventoryTransactionWithReason()
    {
        var (seeded, context, owner, productId) = await SeedWithProductAsync(initialQuantity: 20);

        await new AdjustStockCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new AdjustStockCommand(productId, seeded.BranchId, -3, "Damaged in storage"), CancellationToken.None);

        var transaction = await context.InventoryTransactions
            .Where(t => t.ProductId == productId && t.Type == InventoryTransactionType.Adjustment)
            .SingleAsync();

        Assert.Equal(-3, transaction.QuantityChange);
        Assert.Equal(17, transaction.QuantityAfter);
        Assert.Equal("Damaged in storage", transaction.Reason);
    }

    public void Dispose() => _db.Dispose();
}
