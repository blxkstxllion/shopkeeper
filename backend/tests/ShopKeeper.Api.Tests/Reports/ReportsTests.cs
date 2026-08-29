namespace ShopKeeper.Api.Tests.Reports;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Expenses.Commands;
using ShopKeeper.Application.Products.Commands;
using ShopKeeper.Application.Reports.Queries;
using ShopKeeper.Application.Sales.Commands;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Identity;
using ShopKeeper.Infrastructure.Persistence;

public class ReportsTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    /// <summary>Backdates a just-created sale's CreatedAt - SaveChangesAsync only auto-stamps
    /// CreatedAt on Added (not Modified) entities, so a second save leaves it alone.</summary>
    private static async Task BackdateSaleAsync(AppDbContext context, Guid saleId, DateTimeOffset newCreatedAt)
    {
        var sale = await context.Sales.SingleAsync(s => s.Id == saleId);
        sale.CreatedAt = newCreatedAt;
        await context.SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ProfitabilityReport_ComputesTotalsIncludingExpenses()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var category = await new CreateProductCategoryCommandHandler(context, owner).Handle(
            new CreateProductCategoryCommand("Beverages", null), CancellationToken.None);
        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-RPT", null, null, null, category.Id, 10m, 6m, 10, true, 50, seeded.BranchId),
            CancellationToken.None);

        var sale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 5, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 50m, null)]),
            CancellationToken.None);
        await BackdateSaleAsync(context, sale.Id, new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero));

        var expenseCategory = await new CreateExpenseCategoryCommandHandler(context, owner).Handle(
            new CreateExpenseCategoryCommand("Rent", null), CancellationToken.None);
        await new CreateExpenseCommandHandler(context, owner).Handle(
            new CreateExpenseCommand(seeded.BranchId, expenseCategory.Id, 15m, new DateOnly(2026, 8, 5), null), CancellationToken.None);

        var report = await new GetProfitabilityReportQueryHandler(context, owner).Handle(
            new GetProfitabilityReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), null), CancellationToken.None);

        Assert.Equal(50m, report.Totals.Revenue);
        Assert.Equal(30m, report.Totals.Cogs); // 5 units * 6 cost
        Assert.Equal(20m, report.Totals.GrossProfit); // 50 - 30
        Assert.Equal(15m, report.Totals.TotalExpenses);
        Assert.Equal(5m, report.Totals.NetProfit); // 20 - 15
    }

    [Fact]
    public async Task ProfitabilityReport_ExcludesSalesAndExpensesOutsideRange()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-OOR", null, null, null, null, 10m, 6m, 10, true, 50, seeded.BranchId),
            CancellationToken.None);

        var juneSale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 2, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 20m, null)]),
            CancellationToken.None);
        await BackdateSaleAsync(context, juneSale.Id, new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));

        var augustSale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 3, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 30m, null)]),
            CancellationToken.None);
        await BackdateSaleAsync(context, augustSale.Id, new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero));

        var report = await new GetProfitabilityReportQueryHandler(context, owner).Handle(
            new GetProfitabilityReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), null), CancellationToken.None);

        Assert.Equal(30m, report.Totals.Revenue); // only the August sale
    }

    [Fact]
    public async Task ProfitabilityReport_ByBranch_OnlyPopulatedWhenUnscoped()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var branchB = new Branch { BusinessId = seeded.BusinessId, Name = "Branch B", Code = "B2", Country = "Ghana" };
        context.Branches.Add(branchB);
        await context.SaveChangesAsync(CancellationToken.None);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-BR", null, null, null, null, 10m, 6m, 10, true, 10, seeded.BranchId),
            CancellationToken.None);
        context.ProductStocks.Add(new ProductStock { BusinessId = seeded.BusinessId, ProductId = product.Id, BranchId = branchB.Id, QuantityOnHand = 10 });
        await context.SaveChangesAsync(CancellationToken.None);

        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
            CancellationToken.None);
        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(branchB.Id, [new SaleLineInput(product.Id, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
            CancellationToken.None);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var unscopedReport = await new GetProfitabilityReportQueryHandler(context, owner).Handle(
            new GetProfitabilityReportQuery(today, today, null), CancellationToken.None);
        Assert.Equal(2, unscopedReport.ByBranch.Count);

        var scopedReport = await new GetProfitabilityReportQueryHandler(context, owner).Handle(
            new GetProfitabilityReportQuery(today, today, seeded.BranchId), CancellationToken.None);
        Assert.Empty(scopedReport.ByBranch);
    }

    [Fact]
    public async Task ProfitabilityReport_TopAndWorstProducts_ReflectRealProfit()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var goodProduct = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("High Margin", "SKU-HI", null, null, null, null, 20m, 5m, 10, true, 10, seeded.BranchId),
            CancellationToken.None);
        var badProduct = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Low Margin", "SKU-LO", null, null, null, null, 10m, 9m, 10, true, 10, seeded.BranchId),
            CancellationToken.None);

        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(goodProduct.Id, 2, 0), new SaleLineInput(badProduct.Id, 2, 0)], 0,
                [new SalePaymentInput(PaymentMethod.Cash, 60m, null)]),
            CancellationToken.None);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var report = await new GetProfitabilityReportQueryHandler(context, owner).Handle(
            new GetProfitabilityReportQuery(today, today, null), CancellationToken.None);

        Assert.Equal("High Margin", report.TopProducts[0].ProductName);
        Assert.Equal(30m, report.TopProducts[0].Profit); // (20-5)*2
        Assert.Equal("Low Margin", report.WorstProducts[0].ProductName);
        Assert.Equal(2m, report.WorstProducts[0].Profit); // (10-9)*2
    }

    [Fact]
    public async Task ExpenseReport_ComputesCategoryBreakdownAndDailyTrend()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var rent = await new CreateExpenseCategoryCommandHandler(context, owner).Handle(
            new CreateExpenseCategoryCommand("Rent", null), CancellationToken.None);
        var utilities = await new CreateExpenseCategoryCommandHandler(context, owner).Handle(
            new CreateExpenseCategoryCommand("Utilities", null), CancellationToken.None);

        await new CreateExpenseCommandHandler(context, owner).Handle(
            new CreateExpenseCommand(seeded.BranchId, rent.Id, 800m, new DateOnly(2026, 8, 1), null), CancellationToken.None);
        await new CreateExpenseCommandHandler(context, owner).Handle(
            new CreateExpenseCommand(seeded.BranchId, utilities.Id, 200m, new DateOnly(2026, 8, 2), null), CancellationToken.None);

        var report = await new GetExpenseReportQueryHandler(context, owner).Handle(
            new GetExpenseReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), null, null), CancellationToken.None);

        Assert.Equal(1000m, report.TotalAmount);
        Assert.Equal(2, report.ByCategory.Count);
        Assert.Equal("Rent", report.ByCategory[0].CategoryName);
        Assert.Equal(80m, report.ByCategory[0].PercentOfTotal);

        var day1 = report.DailyTrend.Single(d => d.Date == new DateOnly(2026, 8, 1));
        Assert.Equal(800m, day1.Amount);
    }

    [Fact]
    public async Task InventoryReport_ComputesValuationAndTurnover()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-INV", null, null, null, null, 10m, 6m, 25, true, 20, seeded.BranchId),
            CancellationToken.None);

        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 4, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 40m, null)]),
            CancellationToken.None);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var report = await new GetInventoryReportQueryHandler(context, owner).Handle(
            new GetInventoryReportQuery(today, today, null), CancellationToken.None);

        Assert.Equal(1, report.Valuation.TotalProducts);
        Assert.Equal(1, report.Valuation.LowStockCount); // 16 remaining <= reorder level 25
        Assert.Equal(96m, report.Valuation.InventoryValue); // 16 remaining * 6 cost

        var turnover = Assert.Single(report.Turnover);
        Assert.Equal(4, turnover.UnitsSoldInRange);
        Assert.Equal(16, turnover.QuantityOnHand);
    }

    [Fact]
    public async Task ReportsView_RequiredForAllThreeReports()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var context = _db.CreateContext(seeded.AsOwner());

        var cashier = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BusinessId = seeded.BusinessId,
            BranchId = seeded.BranchId,
            IsOwner = false,
            PermissionsList = ["sales:view", "sales:create"], // no reports:view
        };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new GetProfitabilityReportQueryHandler(context, cashier).Handle(
            new GetProfitabilityReportQuery(today, today, null), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new GetExpenseReportQueryHandler(context, cashier).Handle(
            new GetExpenseReportQuery(today, today, null, null), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new GetInventoryReportQueryHandler(context, cashier).Handle(
            new GetInventoryReportQuery(today, today, null), CancellationToken.None));
    }

    public void Dispose() => _db.Dispose();
}
