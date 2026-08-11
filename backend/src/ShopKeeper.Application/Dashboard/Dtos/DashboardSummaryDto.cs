namespace ShopKeeper.Application.Dashboard.Dtos;

/// <summary>ChangePercent is null when there's no meaningful baseline (previous period was zero) -
/// the frontend should hide the badge rather than show a fabricated "+∞%" or "+0%".</summary>
public record DashboardMetricDto(decimal Value, decimal PreviousValue, decimal? ChangePercent);

public record DailyTrendPointDto(DateOnly Date, decimal Revenue, decimal Profit);

public record CategorySalesDto(string CategoryName, decimal Revenue, decimal PercentOfTotal);

public record TopProductDto(string ProductName, decimal Revenue, int UnitsSold);

public record RecentTransactionDto(
    Guid SaleId, string SaleNumber, string CashierName, decimal Total, string Status, DateTimeOffset CreatedAt);

public record DashboardSummaryDto(
    DashboardMetricDto TodayRevenue,
    DashboardMetricDto TodayProfit,
    DashboardMetricDto MonthRevenue,
    DashboardMetricDto MonthProfit,
    decimal InventoryValue,
    int LowStockCount,
    int OutOfStockCount,
    IReadOnlyList<DailyTrendPointDto> RevenueProfitTrend,
    IReadOnlyList<CategorySalesDto> SalesByCategory,
    IReadOnlyList<TopProductDto> TopProducts,
    IReadOnlyList<RecentTransactionDto> RecentTransactions);
