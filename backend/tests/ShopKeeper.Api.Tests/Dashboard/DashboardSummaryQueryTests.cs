namespace ShopKeeper.Api.Tests.Dashboard;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Dashboard.Queries;
using ShopKeeper.Application.Products.Commands;
using ShopKeeper.Application.Sales.Commands;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Identity;
using ShopKeeper.Infrastructure.Persistence;

public class DashboardSummaryQueryTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    private async Task<(PosTestFixture.SeededBusiness Seeded, AppDbContext Context, TestCurrentUserService Owner, Guid ProductId)> SeedWithProductAsync(
        decimal sellingPrice = 10m, decimal costPrice = 6m, int initialQuantity = 20, int minimumStock = 5)
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var category = await new CreateProductCategoryCommandHandler(context, owner).Handle(
            new CreateProductCategoryCommand("Beverages", null), CancellationToken.None);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-DASH", null, null, null, category.Id, sellingPrice, costPrice, minimumStock, true, initialQuantity, seeded.BranchId),
            CancellationToken.None);

        return (seeded, context, owner, product.Id);
    }

    /// <summary>Backdates a just-created sale's CreatedAt - SaveChangesAsync only auto-stamps
    /// CreatedAt on Added entities, so a second save on a Modified entity leaves it alone.</summary>
    private static async Task BackdateSaleAsync(AppDbContext context, Guid saleId, DateTimeOffset newCreatedAt)
    {
        var sale = await context.Sales.SingleAsync(s => s.Id == saleId);
        sale.CreatedAt = newCreatedAt;
        await context.SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ComputesTodayRevenueAndDayOverDayChange()
    {
        var (seeded, context, owner, productId) = await SeedWithProductAsync(sellingPrice: 10m, costPrice: 6m);

        var yesterdaySale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(productId, 2, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 20m, null)]),
            CancellationToken.None);
        await BackdateSaleAsync(context, yesterdaySale.Id, DateTimeOffset.UtcNow.AddDays(-1));

        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(productId, 5, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 50m, null)]),
            CancellationToken.None);

        var summary = await new GetDashboardSummaryQueryHandler(context, owner).Handle(new GetDashboardSummaryQuery(null), CancellationToken.None);

        Assert.Equal(50m, summary.TodayRevenue.Value);
        Assert.Equal(20m, summary.TodayRevenue.PreviousValue);
        Assert.Equal(150m, summary.TodayRevenue.ChangePercent); // (50-20)/20 * 100
    }

    [Fact]
    public async Task Handle_WithNoPreviousPeriodActivity_ReturnsNullChangePercent()
    {
        var (seeded, context, owner, productId) = await SeedWithProductAsync();

        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(productId, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
            CancellationToken.None);

        var summary = await new GetDashboardSummaryQueryHandler(context, owner).Handle(new GetDashboardSummaryQuery(null), CancellationToken.None);

        Assert.Null(summary.TodayRevenue.ChangePercent);
    }

    [Fact]
    public async Task Handle_ExcludesVoidedSalesFromRevenue()
    {
        var (seeded, context, owner, productId) = await SeedWithProductAsync();

        var sale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(productId, 2, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 20m, null)]),
            CancellationToken.None);
        await new VoidSaleCommandHandler(context, owner).Handle(new VoidSaleCommand(sale.Id, "Mistake"), CancellationToken.None);

        var summary = await new GetDashboardSummaryQueryHandler(context, owner).Handle(new GetDashboardSummaryQuery(null), CancellationToken.None);

        Assert.Equal(0m, summary.TodayRevenue.Value);
    }

    [Fact]
    public async Task Handle_ComputesInventoryValueAndLowStockCount()
    {
        var (seeded, context, owner, _) = await SeedWithProductAsync(costPrice: 6m, initialQuantity: 20, minimumStock: 25);

        var summary = await new GetDashboardSummaryQueryHandler(context, owner).Handle(new GetDashboardSummaryQuery(null), CancellationToken.None);

        Assert.Equal(120m, summary.InventoryValue); // 20 units * 6 cost
        Assert.Equal(1, summary.LowStockCount); // 20 <= reorder level of 25
        Assert.Equal(0, summary.OutOfStockCount);
    }

    [Fact]
    public async Task Handle_TopProductsAndCategoryBreakdown_ReflectRealSales()
    {
        var (seeded, context, owner, productId) = await SeedWithProductAsync(sellingPrice: 10m);

        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(productId, 3, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 30m, null)]),
            CancellationToken.None);

        var summary = await new GetDashboardSummaryQueryHandler(context, owner).Handle(new GetDashboardSummaryQuery(null), CancellationToken.None);

        var topProduct = Assert.Single(summary.TopProducts);
        Assert.Equal("Widget", topProduct.ProductName);
        Assert.Equal(30m, topProduct.Revenue);
        Assert.Equal(3, topProduct.UnitsSold);

        var category = Assert.Single(summary.SalesByCategory);
        Assert.Equal("Beverages", category.CategoryName);
        Assert.Equal(100m, category.PercentOfTotal);
    }

    [Fact]
    public async Task Handle_RecentTransactions_OrderedNewestFirst()
    {
        var (seeded, context, owner, productId) = await SeedWithProductAsync();

        var first = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(productId, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
            CancellationToken.None);
        await BackdateSaleAsync(context, first.Id, DateTimeOffset.UtcNow.AddMinutes(-10));

        var second = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(productId, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
            CancellationToken.None);

        var summary = await new GetDashboardSummaryQueryHandler(context, owner).Handle(new GetDashboardSummaryQuery(null), CancellationToken.None);

        Assert.Equal(2, summary.RecentTransactions.Count);
        Assert.Equal(second.SaleNumber, summary.RecentTransactions[0].SaleNumber);
        Assert.Equal(first.SaleNumber, summary.RecentTransactions[1].SaleNumber);
    }

    [Fact]
    public async Task Handle_ScopedToOneBranch_ExcludesOtherBranchesSales()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var branchB = new Branch { BusinessId = seeded.BusinessId, Name = "Branch B", Code = "B2", Country = "Ghana" };
        context.Branches.Add(branchB);
        await context.SaveChangesAsync(CancellationToken.None);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-BRANCH", null, null, null, null, 10m, 6m, 10, true, 10, seeded.BranchId),
            CancellationToken.None);
        context.ProductStocks.Add(new ProductStock { BusinessId = seeded.BusinessId, ProductId = product.Id, BranchId = branchB.Id, QuantityOnHand = 10 });
        await context.SaveChangesAsync(CancellationToken.None);

        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
            CancellationToken.None);
        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(branchB.Id, [new SaleLineInput(product.Id, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
            CancellationToken.None);

        var summary = await new GetDashboardSummaryQueryHandler(context, owner).Handle(
            new GetDashboardSummaryQuery(seeded.BranchId), CancellationToken.None);

        Assert.Equal(10m, summary.TodayRevenue.Value); // only branch A's sale
        Assert.Single(summary.RecentTransactions);
    }

    public void Dispose() => _db.Dispose();
}
