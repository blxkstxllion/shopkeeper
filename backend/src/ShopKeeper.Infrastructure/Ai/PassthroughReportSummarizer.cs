namespace ShopKeeper.Infrastructure.Ai;

using System.Text;
using ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Builds a deterministic templated paragraph directly from the facts - no LLM call. Used both
/// as the production fallback when Anthropic:ApiKey isn't configured (same "absence never
/// breaks startup" pattern as PassthroughAdvisorNarrator), so a generated report always has a
/// coherent summary whether or not Claude is available.
/// </summary>
public class PassthroughReportSummarizer : IReportSummarizer
{
    public Task<string> SummarizeAsync(ReportFacts f, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.Append($"Revenue for this period was {Money(f.Revenue, f.CurrencyCode)}, with a gross profit of ")
          .Append($"{Money(f.GrossProfit, f.CurrencyCode)} ({f.GrossMarginPercent:0.#}% margin). ")
          .Append($"After {Money(f.TotalExpenses, f.CurrencyCode)} in expenses, net profit was ")
          .Append($"{Money(f.NetProfit, f.CurrencyCode)} ({f.NetMarginPercent:0.#}% margin). ");

        if (f.TopExpenseCategory is not null)
        {
            sb.Append($"The biggest expense category was {f.TopExpenseCategory} ({Money(f.TopExpenseCategoryAmount!.Value, f.CurrencyCode)}). ");
        }
        if (f.TopProduct is not null)
        {
            sb.Append($"{f.TopProduct} was the top-performing product by profit. ");
        }
        if (f.WorstProduct is not null)
        {
            sb.Append($"{f.WorstProduct} was the lowest-performing product by profit. ");
        }
        if (f.OutOfStockCount > 0 || f.LowStockCount > 0)
        {
            sb.Append($"{f.OutOfStockCount} product(s) are out of stock and {f.LowStockCount} are running low.");
        }
        else
        {
            sb.Append("Inventory levels look healthy.");
        }

        return Task.FromResult(sb.ToString().TrimEnd());
    }

    private static string Money(decimal amount, string currencyCode) => $"{currencyCode} {amount:N2}";
}
