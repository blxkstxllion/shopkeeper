namespace ShopKeeper.Api.Tests.Sales;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Products.Commands;
using ShopKeeper.Application.Sales.Commands;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Identity;
using ShopKeeper.Infrastructure.Persistence;

public class SalesCommandTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    private async Task<(PosTestFixture.SeededBusiness Seeded, AppDbContext Context, TestCurrentUserService Owner, Guid ProductId)> SeedWithProductAsync(
        decimal sellingPrice = 10m, decimal costPrice = 6m, int initialQuantity = 20)
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-SALE", null, null, null, null, sellingPrice, costPrice, 5, 10, true, initialQuantity, seeded.BranchId),
            CancellationToken.None);

        return (seeded, context, owner, product.Id);
    }

    [Fact]
    public async Task CreateSale_DeductsStockAndComputesCorrectCogsAndProfit()
    {
        var (seeded, context, owner, productId) = await SeedWithProductAsync(sellingPrice: 10m, costPrice: 6m, initialQuantity: 20);

        var sale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId,
                [new SaleLineInput(productId, 5, 0)],
                0,
                [new SalePaymentInput(PaymentMethod.Cash, 50m, null)]),
            CancellationToken.None);

        Assert.Equal(50m, sale.Subtotal);
        Assert.Equal(30m, sale.TotalCost);
        Assert.Equal(20m, sale.GrossProfit);
        Assert.Equal(50m, sale.Total);
        Assert.Equal("Completed", sale.Status);

        var stock = await context.ProductStocks.SingleAsync(s => s.ProductId == productId);
        Assert.Equal(15, stock.QuantityOnHand);

        var transaction = await context.InventoryTransactions.SingleAsync(t => t.Type == InventoryTransactionType.Sale);
        Assert.Equal(-5, transaction.QuantityChange);
        Assert.Equal(15, transaction.QuantityAfter);
        Assert.Equal("Sale", transaction.ReferenceType);
        Assert.Equal(sale.Id, transaction.ReferenceId);
    }

    [Fact]
    public async Task CreateSale_WithLineDiscount_ReducesRevenueAndProfitButNotCost()
    {
        var (seeded, context, owner, productId) = await SeedWithProductAsync(sellingPrice: 10m, costPrice: 6m, initialQuantity: 20);

        var sale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId,
                [new SaleLineInput(productId, 5, 5m)], // GHS 5 off the line
                0,
                [new SalePaymentInput(PaymentMethod.Cash, 45m, null)]),
            CancellationToken.None);

        Assert.Equal(50m, sale.Subtotal);
        Assert.Equal(5m, sale.DiscountAmount);
        Assert.Equal(45m, sale.Total);
        Assert.Equal(30m, sale.TotalCost); // cost is unaffected by discount
        Assert.Equal(15m, sale.GrossProfit); // (50 - 5) - 30
    }

    [Fact]
    public async Task CreateSale_InsufficientStock_ThrowsConflictAndDoesNotDeductAnything()
    {
        var (seeded, context, owner, productId) = await SeedWithProductAsync(initialQuantity: 3);

        await Assert.ThrowsAsync<ConflictException>(() => new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId,
                [new SaleLineInput(productId, 10, 0)],
                0,
                [new SalePaymentInput(PaymentMethod.Cash, 100m, null)]),
            CancellationToken.None));

        var stock = await context.ProductStocks.SingleAsync(s => s.ProductId == productId);
        Assert.Equal(3, stock.QuantityOnHand);
        Assert.Empty(context.Sales);
    }

    [Fact]
    public async Task CreateSale_PaymentsDoNotMatchTotal_ThrowsConflict()
    {
        var (seeded, context, owner, productId) = await SeedWithProductAsync();

        await Assert.ThrowsAsync<ConflictException>(() => new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId,
                [new SaleLineInput(productId, 5, 0)],
                0,
                [new SalePaymentInput(PaymentMethod.Cash, 30m, null)]), // total should be 50
            CancellationToken.None));
    }

    [Fact]
    public async Task CreateSale_SplitPayment_AcceptsMultiplePaymentsSummingToTotal()
    {
        var (seeded, context, owner, productId) = await SeedWithProductAsync();

        var sale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId,
                [new SaleLineInput(productId, 5, 0)],
                0,
                [
                    new SalePaymentInput(PaymentMethod.Cash, 20m, null),
                    new SalePaymentInput(PaymentMethod.MobileMoney, 30m, "MM-REF-1"),
                ]),
            CancellationToken.None);

        Assert.Equal(2, sale.Payments.Count);
        Assert.Equal(50m, sale.Payments.Sum(p => p.Amount));
    }

    [Fact]
    public async Task VoidSale_RestoresStockAndSetsStatusVoided()
    {
        var (seeded, context, owner, productId) = await SeedWithProductAsync(initialQuantity: 20);

        var sale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(productId, 5, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 50m, null)]),
            CancellationToken.None);

        await new VoidSaleCommandHandler(context, owner).Handle(new VoidSaleCommand(sale.Id, "Rang up wrong item"), CancellationToken.None);

        var stock = await context.ProductStocks.SingleAsync(s => s.ProductId == productId);
        Assert.Equal(20, stock.QuantityOnHand); // back to original

        var storedSale = await context.Sales.SingleAsync(s => s.Id == sale.Id);
        Assert.Equal(SaleStatus.Voided, storedSale.Status);
        Assert.NotNull(storedSale.VoidedAt);
    }

    [Fact]
    public async Task VoidSale_AlreadyVoided_ThrowsConflict()
    {
        var (seeded, context, owner, productId) = await SeedWithProductAsync();

        var sale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(productId, 5, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 50m, null)]),
            CancellationToken.None);

        await new VoidSaleCommandHandler(context, owner).Handle(new VoidSaleCommand(sale.Id, "First void"), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new VoidSaleCommandHandler(context, owner).Handle(new VoidSaleCommand(sale.Id, "Second void"), CancellationToken.None));
    }

    [Fact]
    public async Task RefundSale_PartialQuantity_RestoresStockAndSetsPartiallyRefunded()
    {
        var (seeded, context, owner, productId) = await SeedWithProductAsync(initialQuantity: 20);

        var sale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(productId, 10, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 100m, null)]),
            CancellationToken.None);

        var saleItemId = sale.Items.Single().Id;

        var refund = await new RefundSaleCommandHandler(context, owner).Handle(
            new RefundSaleCommand(sale.Id, [new RefundLineInput(saleItemId, 3)], "Customer returned 3 units"), CancellationToken.None);

        Assert.Equal(30m, refund.TotalAmount); // 3 * unit price of 10

        var stock = await context.ProductStocks.SingleAsync(s => s.ProductId == productId);
        Assert.Equal(13, stock.QuantityOnHand); // 20 - 10 (sale) + 3 (refund)

        var storedSale = await context.Sales.SingleAsync(s => s.Id == sale.Id);
        Assert.Equal(SaleStatus.PartiallyRefunded, storedSale.Status);
    }

    [Fact]
    public async Task RefundSale_FullQuantity_SetsStatusRefunded()
    {
        var (seeded, context, owner, productId) = await SeedWithProductAsync();

        var sale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(productId, 5, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 50m, null)]),
            CancellationToken.None);

        var saleItemId = sale.Items.Single().Id;

        await new RefundSaleCommandHandler(context, owner).Handle(
            new RefundSaleCommand(sale.Id, [new RefundLineInput(saleItemId, 5)], "Full return"), CancellationToken.None);

        var storedSale = await context.Sales.SingleAsync(s => s.Id == sale.Id);
        Assert.Equal(SaleStatus.Refunded, storedSale.Status);
    }

    [Fact]
    public async Task RefundSale_ExceedingRefundableQuantity_ThrowsConflict()
    {
        var (seeded, context, owner, productId) = await SeedWithProductAsync();

        var sale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(productId, 5, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 50m, null)]),
            CancellationToken.None);

        var saleItemId = sale.Items.Single().Id;

        await Assert.ThrowsAsync<ConflictException>(() => new RefundSaleCommandHandler(context, owner).Handle(
            new RefundSaleCommand(sale.Id, [new RefundLineInput(saleItemId, 6)], "Too many"), CancellationToken.None));
    }

    [Fact]
    public async Task CreateSale_ReplayedWithSameClientRequestId_ReturnsOriginalSale_DoesNotDoubleSell()
    {
        var (seeded, context, owner, productId) = await SeedWithProductAsync(initialQuantity: 20);
        var clientRequestId = Guid.NewGuid();

        var first = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId, [new SaleLineInput(productId, 5, 0)], 0,
                [new SalePaymentInput(PaymentMethod.Cash, 50m, null)], ClientRequestId: clientRequestId),
            CancellationToken.None);

        // Simulates the offline sync engine retrying because the first response never arrived,
        // even though the sale actually was created - same ClientRequestId, same request.
        var replay = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId, [new SaleLineInput(productId, 5, 0)], 0,
                [new SalePaymentInput(PaymentMethod.Cash, 50m, null)], ClientRequestId: clientRequestId),
            CancellationToken.None);

        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(first.SaleNumber, replay.SaleNumber);

        Assert.Single(await context.Sales.AsNoTracking().ToListAsync());
        var stock = await context.ProductStocks.AsNoTracking().SingleAsync(s => s.ProductId == productId);
        Assert.Equal(15, stock.QuantityOnHand); // stock only decremented once, not twice
    }

    [Fact]
    public async Task CreateSale_GenuineConcurrentReplay_ExactlyOneSaleCreated_BothCallersGetSameSale()
    {
        using var db = new ConcurrentSqliteTestDatabase();
        var hasher = new BcryptPasswordHasher();
        var jwt = new JwtTokenService(Options.Create(PosTestFixture.JwtTestSettings));
        var seeded = await PosTestFixture.SeedAsync(db, hasher, jwt);
        var owner = seeded.AsOwner();
        var setupContext = db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(setupContext, owner, new PlanLimitService(setupContext)).Handle(
            new CreateProductCommand("Widget", "SKU-IDEMPOTENT", null, null, null, null, 10m, 6m, 0, 0, true, 20, seeded.BranchId),
            CancellationToken.None);

        var clientRequestId = Guid.NewGuid();

        Task<ShopKeeper.Application.Sales.Dtos.SaleDto> Send()
        {
            var context = db.CreateContext(owner);
            return new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
                new CreateSaleCommand(
                    seeded.BranchId, [new SaleLineInput(product.Id, 5, 0)], 0,
                    [new SalePaymentInput(PaymentMethod.Cash, 50m, null)], ClientRequestId: clientRequestId),
                CancellationToken.None);
        }

        // Two genuinely concurrent requests carrying the identical client key - exactly what
        // happens if the sync engine's retry fires before the first attempt's response lands.
        var results = await Task.WhenAll(Send(), Send());

        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Single(await setupContext.Sales.AsNoTracking().ToListAsync());
        var stock = await setupContext.ProductStocks.AsNoTracking().SingleAsync(s => s.ProductId == product.Id);
        Assert.Equal(15, stock.QuantityOnHand);
    }

    [Fact]
    public async Task CreateSale_WithoutClientRequestId_StillWorksUnchanged()
    {
        var (seeded, context, owner, productId) = await SeedWithProductAsync(initialQuantity: 20);

        var saleA = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(productId, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
            CancellationToken.None);
        var saleB = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(productId, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
            CancellationToken.None);

        Assert.NotEqual(saleA.Id, saleB.Id); // two genuinely separate sales, both without a client key
        Assert.Equal(2, await context.Sales.AsNoTracking().CountAsync());
    }

    public void Dispose() => _db.Dispose();
}
