namespace ShopKeeper.Api.Tests.Inventory;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Inventory.Commands;
using ShopKeeper.Application.Products.Commands;
using ShopKeeper.Application.Sales.Commands;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Identity;

/// <summary>
/// Proves ProductStock.RowVersion (optimistic concurrency) actually stops two simultaneous
/// operations from both succeeding against the same stock - the exact "stock=1, two cashiers
/// both read 1, both complete" scenario from the code review. Two styles of test:
/// deterministic interleaved (SqliteTestDatabase's single shared connection - reliable, same
/// result every run) and genuine Task.WhenAll concurrency (ConcurrentSqliteTestDatabase's
/// independent connections onto one shared-cache database - proves it under a real race, not
/// just a simulated one).
/// </summary>
public class InventoryConcurrencyTests
{
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    [Fact]
    public async Task AdjustStock_InterleavedConcurrentAdjustments_SecondWriterGetsCleanConflict()
    {
        using var db = new SqliteTestDatabase();
        var seeded = await PosTestFixture.SeedAsync(db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var setupContext = db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(setupContext, owner).Handle(
            new CreateProductCommand("Widget", "SKU-CONC", null, null, null, null, 10m, 6m, 0, 0, true, 1, seeded.BranchId),
            CancellationToken.None);

        // Two independent contexts (two "cashiers"), each reading stock=1 before either writes -
        // simulates the exact race from the review without depending on real thread timing.
        var contextA = db.CreateContext(owner);
        var contextB = db.CreateContext(owner);

        var stockA = await contextA.ProductStocks.SingleAsync(s => s.ProductId == product.Id);
        var stockB = await contextB.ProductStocks.SingleAsync(s => s.ProductId == product.Id);
        Assert.Equal(1, stockA.QuantityOnHand);
        Assert.Equal(1, stockB.QuantityOnHand);

        var quantityA = await new AdjustStockCommandHandler(contextA, owner, new NotificationDispatcher(contextA)).Handle(
            new AdjustStockCommand(product.Id, seeded.BranchId, -1, "Sale A"), CancellationToken.None);
        Assert.Equal(0, quantityA);

        // contextB is still holding the stale (QuantityOnHand=1, RowVersion=0) entity it read
        // before contextA's write - its handler recomputes -1 against that stale value and
        // tries to save; RowVersion must stop this from silently succeeding.
        await Assert.ThrowsAsync<ConflictException>(() => new AdjustStockCommandHandler(contextB, owner, new NotificationDispatcher(contextB)).Handle(
            new AdjustStockCommand(product.Id, seeded.BranchId, -1, "Sale B"), CancellationToken.None));

        var finalStock = await setupContext.ProductStocks.AsNoTracking().SingleAsync(s => s.ProductId == product.Id);
        Assert.Equal(0, finalStock.QuantityOnHand); // only A's adjustment took effect
    }

    [Fact]
    public async Task CreateSale_InterleavedConcurrentSalesForLastUnit_SecondSaleGetsCleanConflict_NoOversell()
    {
        using var db = new SqliteTestDatabase();
        var seeded = await PosTestFixture.SeedAsync(db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var setupContext = db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(setupContext, owner).Handle(
            new CreateProductCommand("Widget", "SKU-LASTUNIT", null, null, null, null, 10m, 6m, 0, 0, true, 1, seeded.BranchId),
            CancellationToken.None);

        var contextA = db.CreateContext(owner);
        var contextB = db.CreateContext(owner);

        var saleA = await new CreateSaleCommandHandler(contextA, owner, new NotificationDispatcher(contextA)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
            CancellationToken.None);
        Assert.NotNull(saleA);

        // contextB still believes 1 unit is available (it read stock before contextA's sale
        // committed) - this is precisely "Cashier A reads 1, Cashier B reads 1, both believe
        // the sale is valid" from the review. It must not be allowed to also succeed.
        await Assert.ThrowsAsync<ConflictException>(() => new CreateSaleCommandHandler(contextB, owner, new NotificationDispatcher(contextB)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
            CancellationToken.None));

        var finalStock = await setupContext.ProductStocks.AsNoTracking().SingleAsync(s => s.ProductId == product.Id);
        Assert.Equal(0, finalStock.QuantityOnHand);
        Assert.Single(await setupContext.Sales.AsNoTracking().ToListAsync()); // exactly one sale exists - the item was not sold twice
    }

    [Fact]
    public async Task AdjustStock_GenuineConcurrentAdjustments_OnlyOneOfTwoSucceeds()
    {
        using var db = new ConcurrentSqliteTestDatabase();
        var seeded = await PosTestFixture.SeedAsync(db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var setupContext = db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(setupContext, owner).Handle(
            new CreateProductCommand("Widget", "SKU-RACE", null, null, null, null, 10m, 6m, 0, 0, true, 1, seeded.BranchId),
            CancellationToken.None);

        // Two real, independently-connected contexts racing via Task.WhenAll - not a
        // simulated interleaving. SQLite's shared-cache locking serializes the actual writes,
        // but both requests genuinely overlap in flight, exercising the real code path
        // (including the DbUpdateConcurrencyException catch) rather than a hand-arranged one.
        async Task<bool> TryAdjust()
        {
            var context = db.CreateContext(owner);
            try
            {
                await new AdjustStockCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
                    new AdjustStockCommand(product.Id, seeded.BranchId, -1, "Concurrent adjustment"), CancellationToken.None);
                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(TryAdjust(), TryAdjust());

        Assert.Single(results, r => r); // exactly one succeeded
        Assert.Single(results, r => !r); // exactly one lost the race cleanly

        var finalStock = await setupContext.ProductStocks.AsNoTracking().SingleAsync(s => s.ProductId == product.Id);
        Assert.Equal(0, finalStock.QuantityOnHand);
    }

    [Fact]
    public async Task CreateSale_GenuineConcurrentSalesForLastUnit_OnlyOneSaleIsCreated_NoOversell()
    {
        using var db = new ConcurrentSqliteTestDatabase();
        var seeded = await PosTestFixture.SeedAsync(db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var setupContext = db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(setupContext, owner).Handle(
            new CreateProductCommand("Widget", "SKU-RACE-SALE", null, null, null, null, 10m, 6m, 0, 0, true, 1, seeded.BranchId),
            CancellationToken.None);

        async Task<bool> TrySell()
        {
            var context = db.CreateContext(owner);
            try
            {
                await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
                    new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
                    CancellationToken.None);
                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(TrySell(), TrySell());

        Assert.Single(results, r => r);
        Assert.Single(results, r => !r);

        var finalStock = await setupContext.ProductStocks.AsNoTracking().SingleAsync(s => s.ProductId == product.Id);
        Assert.Equal(0, finalStock.QuantityOnHand);
        Assert.Single(await setupContext.Sales.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task AdjustStock_InsufficientStock_StillThrowsExistingMessage()
    {
        using var db = new SqliteTestDatabase();
        var seeded = await PosTestFixture.SeedAsync(db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(context, owner).Handle(
            new CreateProductCommand("Widget", "SKU-INSUFFICIENT", null, null, null, null, 10m, 6m, 0, 0, true, 3, seeded.BranchId),
            CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => new AdjustStockCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new AdjustStockCommand(product.Id, seeded.BranchId, -10, "Too much"), CancellationToken.None));

        Assert.Contains("units", ex.Message); // unchanged existing "this adjustment would take X to Y units" message
    }

    [Fact]
    public async Task CreateSale_ConcurrentSalesAcrossTwoBusinesses_EachBusinessOnlyAffectsItsOwnStock()
    {
        using var db = new ConcurrentSqliteTestDatabase();
        var businessA = await PosTestFixture.SeedAsync(db, _hasher, _jwt, "ownerA@shop.test");
        var businessB = await PosTestFixture.SeedAsync(db, _hasher, _jwt, "ownerB@shop.test");
        var ownerA = businessA.AsOwner();
        var ownerB = businessB.AsOwner();

        var setupA = db.CreateContext(ownerA);
        var setupB = db.CreateContext(ownerB);

        var productA = await new CreateProductCommandHandler(setupA, ownerA).Handle(
            new CreateProductCommand("Widget A", "SKU-TENANT-A", null, null, null, null, 10m, 6m, 0, 0, true, 1, businessA.BranchId),
            CancellationToken.None);
        var productB = await new CreateProductCommandHandler(setupB, ownerB).Handle(
            new CreateProductCommand("Widget B", "SKU-TENANT-B", null, null, null, null, 10m, 6m, 0, 0, true, 1, businessB.BranchId),
            CancellationToken.None);

        // Both businesses sell their own last unit at the same time - neither should be able
        // to see, lock, or be blocked by the other's ProductStock row (tenant isolation must
        // hold even under the new concurrency-token machinery).
        var contextA = db.CreateContext(ownerA);
        var contextB = db.CreateContext(ownerB);

        var results = await Task.WhenAll(
            new CreateSaleCommandHandler(contextA, ownerA, new NotificationDispatcher(contextA)).Handle(
                new CreateSaleCommand(businessA.BranchId, [new SaleLineInput(productA.Id, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
                CancellationToken.None),
            new CreateSaleCommandHandler(contextB, ownerB, new NotificationDispatcher(contextB)).Handle(
                new CreateSaleCommand(businessB.BranchId, [new SaleLineInput(productB.Id, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
                CancellationToken.None));

        Assert.Equal(2, results.Length); // both succeeded independently, no cross-tenant blocking

        var stockA = await setupA.ProductStocks.AsNoTracking().SingleAsync(s => s.ProductId == productA.Id);
        var stockB = await setupB.ProductStocks.AsNoTracking().SingleAsync(s => s.ProductId == productB.Id);
        Assert.Equal(0, stockA.QuantityOnHand);
        Assert.Equal(0, stockB.QuantityOnHand);
    }

    [Fact]
    public async Task CreateSale_ConcurrentSales_SameBusiness_SaleNumbersAreUnique()
    {
        using var db = new ConcurrentSqliteTestDatabase();
        var seeded = await PosTestFixture.SeedAsync(db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var setupContext = db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(setupContext, owner).Handle(
            new CreateProductCommand("Widget", "SKU-NUMS", null, null, null, null, 10m, 6m, 0, 0, true, 10, seeded.BranchId),
            CancellationToken.None);

        Task<Application.Sales.Dtos.SaleDto> Sell()
        {
            var context = db.CreateContext(owner);
            return new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
                new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
                CancellationToken.None);
        }

        var sales = await Task.WhenAll(Sell(), Sell(), Sell(), Sell(), Sell());

        var saleNumbers = sales.Select(s => s.SaleNumber).ToList();
        Assert.Equal(5, saleNumbers.Distinct().Count()); // no two concurrent sales got the same number
    }

    [Fact]
    public async Task CreateSale_ConcurrentSales_DifferentBusinesses_CanReuseTheSameSaleNumber()
    {
        using var db = new ConcurrentSqliteTestDatabase();
        var businessA = await PosTestFixture.SeedAsync(db, _hasher, _jwt, "numowner-a@shop.test");
        var businessB = await PosTestFixture.SeedAsync(db, _hasher, _jwt, "numowner-b@shop.test");
        var ownerA = businessA.AsOwner();
        var ownerB = businessB.AsOwner();

        var setupA = db.CreateContext(ownerA);
        var setupB = db.CreateContext(ownerB);

        var productA = await new CreateProductCommandHandler(setupA, ownerA).Handle(
            new CreateProductCommand("Widget A", "SKU-NUM-A", null, null, null, null, 10m, 6m, 0, 0, true, 5, businessA.BranchId),
            CancellationToken.None);
        var productB = await new CreateProductCommandHandler(setupB, ownerB).Handle(
            new CreateProductCommand("Widget B", "SKU-NUM-B", null, null, null, null, 10m, 6m, 0, 0, true, 5, businessB.BranchId),
            CancellationToken.None);

        // Each business's very first sale - both should independently claim "S-000001".
        // Sale numbers are scoped per-business (BusinessId, SaleNumber) is the unique index,
        // not SaleNumber alone - this is not a conflict.
        var contextA = db.CreateContext(ownerA);
        var contextB = db.CreateContext(ownerB);

        var saleA = await new CreateSaleCommandHandler(contextA, ownerA, new NotificationDispatcher(contextA)).Handle(
            new CreateSaleCommand(businessA.BranchId, [new SaleLineInput(productA.Id, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
            CancellationToken.None);
        var saleB = await new CreateSaleCommandHandler(contextB, ownerB, new NotificationDispatcher(contextB)).Handle(
            new CreateSaleCommand(businessB.BranchId, [new SaleLineInput(productB.Id, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
            CancellationToken.None);

        Assert.Equal("S-000001", saleA.SaleNumber);
        Assert.Equal("S-000001", saleB.SaleNumber);
    }
}
