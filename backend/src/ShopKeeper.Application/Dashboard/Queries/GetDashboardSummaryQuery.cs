namespace ShopKeeper.Application.Dashboard.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Dashboard.Dtos;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Enums;

public record GetDashboardSummaryQuery(Guid? BranchId) : IRequest<DashboardSummaryDto>;

/// <summary>
/// Every number here is a real aggregate over this business's own Sales/ProductStocks - never
/// a placeholder. Deliberately excludes anything we can't back with real data yet (business
/// health score, cash flow, pending orders, AI insights - see later phases) rather than
/// showing a plausible-looking fake value.
///
/// All date-bucketing happens in memory after a single DB round trip, not via SQL date-range
/// predicates: EF Core's SQLite provider (used only by this project's test suite) can't
/// translate DateTimeOffset comparisons, and at this app's current scale materializing a
/// business's sales once and slicing them with LINQ is simpler than maintaining two different
/// query shapes for SQLite vs Postgres. Revisit if a single business's sale volume ever makes
/// that materialization itself the bottleneck.
/// </summary>
public class GetDashboardSummaryQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.SalesView);
        if (request.BranchId.HasValue)
        {
            currentUser.RequireBranchAccess(request.BranchId.Value);
        }
        var branchId = request.BranchId ?? currentUser.BranchId;

        var salesQuery = db.Sales
            .Include(s => s.CashierUser)
            .Include(s => s.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.Category)
            .AsQueryable();

        if (branchId.HasValue)
        {
            salesQuery = salesQuery.Where(s => s.BranchId == branchId);
        }

        var allSales = await salesQuery.ToListAsync(cancellationToken);
        var nonVoided = allSales.Where(s => s.Status != SaleStatus.Voided).ToList();

        var now = DateTimeOffset.UtcNow;
        var todayStart = new DateTimeOffset(now.Date, TimeSpan.Zero);
        var yesterdayStart = todayStart.AddDays(-1);
        var monthStart = new DateTimeOffset(new DateTime(now.Year, now.Month, 1), TimeSpan.Zero);
        var prevMonthStart = monthStart.AddMonths(-1);

        var todaySales = nonVoided.Where(s => s.CreatedAt >= todayStart).ToList();
        var yesterdaySales = nonVoided.Where(s => s.CreatedAt >= yesterdayStart && s.CreatedAt < todayStart).ToList();
        var monthSales = nonVoided.Where(s => s.CreatedAt >= monthStart).ToList();
        var prevMonthSales = nonVoided.Where(s => s.CreatedAt >= prevMonthStart && s.CreatedAt < monthStart).ToList();

        var todayRevenue = BuildMetric(todaySales.Sum(s => s.Total), yesterdaySales.Sum(s => s.Total));
        var todayProfit = BuildMetric(todaySales.Sum(s => s.GrossProfit), yesterdaySales.Sum(s => s.GrossProfit));
        var monthRevenue = BuildMetric(monthSales.Sum(s => s.Total), prevMonthSales.Sum(s => s.Total));
        var monthProfit = BuildMetric(monthSales.Sum(s => s.GrossProfit), prevMonthSales.Sum(s => s.GrossProfit));

        var trend = new List<DailyTrendPointDto>();
        for (var i = 6; i >= 0; i--)
        {
            var day = now.Date.AddDays(-i);
            var dayStart = new DateTimeOffset(day, TimeSpan.Zero);
            var dayEnd = dayStart.AddDays(1);
            var daySales = nonVoided.Where(s => s.CreatedAt >= dayStart && s.CreatedAt < dayEnd).ToList();
            trend.Add(new DailyTrendPointDto(DateOnly.FromDateTime(day), daySales.Sum(s => s.Total), daySales.Sum(s => s.GrossProfit)));
        }

        var categoryGroups = todaySales
            .SelectMany(s => s.Items)
            .GroupBy(i => i.Product?.Category?.Name ?? "Uncategorized")
            .Select(g => (Category: g.Key, Revenue: g.Sum(i => i.LineRevenue)))
            .OrderByDescending(g => g.Revenue)
            .ToList();
        var categoryTotal = categoryGroups.Sum(g => g.Revenue);
        var salesByCategory = categoryGroups
            .Select(g => new CategorySalesDto(g.Category, g.Revenue, categoryTotal > 0 ? Math.Round(g.Revenue / categoryTotal * 100, 1) : 0))
            .ToList();

        var topProducts = monthSales
            .SelectMany(s => s.Items)
            .GroupBy(i => i.ProductNameSnapshot)
            .Select(g => new TopProductDto(g.Key, g.Sum(i => i.LineRevenue), g.Sum(i => i.Quantity)))
            .OrderByDescending(p => p.Revenue)
            .Take(5)
            .ToList();

        var recentTransactions = allSales
            .OrderByDescending(s => s.CreatedAt)
            .Take(5)
            .Select(s => new RecentTransactionDto(
                s.Id, s.SaleNumber, $"{s.CashierUser.FirstName} {s.CashierUser.LastName}", s.Total, s.Status.ToString(), s.CreatedAt))
            .ToList();

        var stockQuery = db.ProductStocks.Include(ps => ps.Product).Where(ps => ps.Product.IsActive);
        if (branchId.HasValue)
        {
            stockQuery = stockQuery.Where(ps => ps.BranchId == branchId);
        }
        var stocks = await stockQuery.ToListAsync(cancellationToken);

        var inventoryValue = stocks.Sum(s => s.QuantityOnHand * s.Product.CostPrice);
        var lowStockCount = stocks.Count(s => s.QuantityOnHand > 0 && s.QuantityOnHand <= s.Product.ReorderLevel);
        var outOfStockCount = stocks.Count(s => s.QuantityOnHand == 0);

        return new DashboardSummaryDto(
            todayRevenue, todayProfit, monthRevenue, monthProfit,
            inventoryValue, lowStockCount, outOfStockCount,
            trend, salesByCategory, topProducts, recentTransactions);
    }

    private static DashboardMetricDto BuildMetric(decimal current, decimal previous)
    {
        decimal? changePercent = previous == 0 ? null : Math.Round((current - previous) / Math.Abs(previous) * 100, 1);
        return new DashboardMetricDto(current, previous, changePercent);
    }
}
