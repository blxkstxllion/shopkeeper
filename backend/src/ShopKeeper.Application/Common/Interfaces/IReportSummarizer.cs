namespace ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Writes a natural-language executive summary from an already-computed set of key business
/// numbers - never given raw report data, only these facts, so a summary can never state a
/// number that isn't already verified elsewhere in the same exported document. Mirrors
/// IAdvisorNarrator's "narrate, don't compute" shape.
/// </summary>
public interface IReportSummarizer
{
    Task<string> SummarizeAsync(ReportFacts facts, CancellationToken ct = default);
}

public record ReportFacts(
    string CurrencyCode,
    decimal Revenue,
    decimal Cogs,
    decimal GrossProfit,
    decimal GrossMarginPercent,
    decimal TotalExpenses,
    decimal NetProfit,
    decimal NetMarginPercent,
    string? TopExpenseCategory,
    decimal? TopExpenseCategoryAmount,
    string? TopProduct,
    decimal? TopProductProfit,
    string? WorstProduct,
    decimal? WorstProductProfit,
    int LowStockCount,
    int OutOfStockCount);
