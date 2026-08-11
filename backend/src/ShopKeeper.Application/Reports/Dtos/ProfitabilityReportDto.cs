namespace ShopKeeper.Application.Reports.Dtos;

public record ProfitabilityTotalsDto(
    decimal Revenue,
    decimal Cogs,
    decimal GrossProfit,
    decimal GrossMarginPercent,
    decimal TotalExpenses,
    decimal NetProfit,
    decimal NetMarginPercent);

public record DailyProfitPointDto(DateOnly Date, decimal Revenue, decimal NetProfit);

public record CategoryProfitDto(string CategoryName, decimal Revenue, decimal Profit);

public record BranchProfitDto(Guid BranchId, string BranchName, decimal Revenue, decimal Profit, decimal MarginPercent);

public record CashierProfitDto(Guid CashierUserId, string CashierName, decimal Revenue, decimal Profit, int SalesCount);

public record ProductProfitDto(string ProductName, decimal Revenue, decimal Profit, int UnitsSold);

public record ProfitabilityReportDto(
    ProfitabilityTotalsDto Totals,
    IReadOnlyList<DailyProfitPointDto> DailyTrend,
    IReadOnlyList<CategoryProfitDto> ByCategory,
    IReadOnlyList<BranchProfitDto> ByBranch,
    IReadOnlyList<CashierProfitDto> ByCashier,
    IReadOnlyList<ProductProfitDto> TopProducts,
    IReadOnlyList<ProductProfitDto> WorstProducts);
