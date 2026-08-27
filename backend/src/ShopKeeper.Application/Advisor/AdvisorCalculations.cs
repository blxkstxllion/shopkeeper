namespace ShopKeeper.Application.Advisor;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Dashboard.Queries;
using ShopKeeper.Application.Reports.Queries;

/// <summary>
/// Computes the grounded, already-correct answer for one of the 8 closed Advisor questions, by
/// calling the same MediatR queries Dashboard/Reports already use and formatting their DTOs into
/// a sentence - no separate aggregation logic, so the advisor's numbers can never disagree with
/// what those pages show. Shared by both the fixed-button path (GetAdvisorAnswerQueryHandler) and
/// the free-text path (AskAdvisorCommandHandler) - in the free-text case, Claude only ever
/// *selects* which of these 8 to run (via tool-calling), it never computes a number itself.
/// </summary>
public class AdvisorCalculations(ISender mediator, IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task<string> ComputeAsync(AdvisorQuestionId questionId, Guid? branchId, CancellationToken ct)
    {
        var businessId = currentUser.RequireBusinessId();
        var currencyCode = await db.Businesses.Where(b => b.Id == businessId).Select(b => b.CurrencyCode).FirstAsync(ct);

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        return questionId switch
        {
            AdvisorQuestionId.RevenueThisMonth => await AnswerRevenueThisMonth(branchId, currencyCode, ct),
            AdvisorQuestionId.ProfitMargin => await AnswerProfitMargin(branchId, monthStart, today, currencyCode, ct),
            AdvisorQuestionId.LowStock => await AnswerLowStock(branchId, monthStart, today, ct),
            AdvisorQuestionId.BestSellingProduct => await AnswerBestSellingProduct(branchId, currencyCode, ct),
            AdvisorQuestionId.WorstPerformingProduct => await AnswerWorstPerformingProduct(branchId, monthStart, today, currencyCode, ct),
            AdvisorQuestionId.BranchComparison => await AnswerBranchComparison(monthStart, today, currencyCode, ct),
            AdvisorQuestionId.TopExpenseCategories => await AnswerTopExpenseCategories(branchId, monthStart, today, currencyCode, ct),
            AdvisorQuestionId.AmIProfitable => await AnswerAmIProfitable(branchId, monthStart, today, currencyCode, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(questionId), $"Unknown question id '{questionId}'."),
        };
    }

    private async Task<string> AnswerRevenueThisMonth(Guid? branchId, string currency, CancellationToken ct)
    {
        var summary = await mediator.Send(new GetDashboardSummaryQuery(branchId), ct);
        var trend = summary.MonthRevenue.ChangePercent is { } pct
            ? $"{(pct >= 0 ? "up" : "down")} {Math.Abs(pct):0.#}% from last month"
            : "no comparable data from last month yet";
        return $"Revenue this month is {Money(summary.MonthRevenue.Value, currency)}, {trend}. " +
               $"Profit for the month is {Money(summary.MonthProfit.Value, currency)}.";
    }

    private async Task<string> AnswerProfitMargin(Guid? branchId, DateOnly from, DateOnly to, string currency, CancellationToken ct)
    {
        var report = await mediator.Send(new GetProfitabilityReportQuery(from, to, branchId), ct);
        if (report.Totals.Revenue == 0)
        {
            return "There's no revenue recorded this month yet, so there's no margin to calculate.";
        }
        return $"Gross margin this month is {report.Totals.GrossMarginPercent:0.#}%. " +
               $"After expenses, net margin is {report.Totals.NetMarginPercent:0.#}% (net profit {Money(report.Totals.NetProfit, currency)}).";
    }

    private async Task<string> AnswerLowStock(Guid? branchId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var report = await mediator.Send(new GetInventoryReportQuery(from, to, branchId), ct);
        if (report.LowStockProducts.Count == 0 && report.OutOfStockProducts.Count == 0)
        {
            return "Nothing is low on stock right now - inventory looks healthy.";
        }

        var parts = new List<string>();
        if (report.OutOfStockProducts.Count > 0)
        {
            var names = string.Join(", ", report.OutOfStockProducts.Take(5).Select(p => p.ProductName));
            parts.Add($"{report.OutOfStockProducts.Count} product(s) out of stock: {names}");
        }
        if (report.LowStockProducts.Count > 0)
        {
            var names = string.Join(", ", report.LowStockProducts.Take(5).Select(p => $"{p.ProductName} ({p.QuantityOnHand} left)"));
            parts.Add($"{report.LowStockProducts.Count} product(s) low on stock: {names}");
        }
        return string.Join(". ", parts) + ".";
    }

    private async Task<string> AnswerBestSellingProduct(Guid? branchId, string currency, CancellationToken ct)
    {
        var summary = await mediator.Send(new GetDashboardSummaryQuery(branchId), ct);
        var top = summary.TopProducts.FirstOrDefault();
        return top is null
            ? "No sales recorded yet this month."
            : $"Your best-selling product this month is {top.ProductName}, with {top.UnitsSold} unit(s) sold and {Money(top.Revenue, currency)} in revenue.";
    }

    private async Task<string> AnswerWorstPerformingProduct(Guid? branchId, DateOnly from, DateOnly to, string currency, CancellationToken ct)
    {
        var report = await mediator.Send(new GetProfitabilityReportQuery(from, to, branchId), ct);
        var worst = report.WorstProducts.FirstOrDefault();
        return worst is null
            ? "No sales recorded yet this month."
            : $"Your lowest-profit product this month is {worst.ProductName}, contributing {Money(worst.Profit, currency)} in profit from {worst.UnitsSold} unit(s) sold.";
    }

    private async Task<string> AnswerBranchComparison(DateOnly from, DateOnly to, string currency, CancellationToken ct)
    {
        // GetProfitabilityReportQueryHandler only populates ByBranch for an unscoped caller
        // (no fixed BranchId) - check that directly rather than inferring scoping from an
        // empty list, which is ambiguous with "no branch has any sales yet this month."
        if (currentUser.BranchId.HasValue)
        {
            return "Branch comparison isn't available for your account - you're scoped to a single branch.";
        }

        var report = await mediator.Send(new GetProfitabilityReportQuery(from, to, null), ct);
        if (report.ByBranch.Count == 0)
        {
            return "No branch has recorded any sales yet this month.";
        }
        if (report.ByBranch.Count == 1)
        {
            var only = report.ByBranch[0];
            return $"You have one branch, {only.BranchName}, with {Money(only.Revenue, currency)} in revenue and {Money(only.Profit, currency)} in profit this month.";
        }
        var ranked = string.Join(", ", report.ByBranch.Select(b => $"{b.BranchName} ({Money(b.Profit, currency)} profit, {b.MarginPercent:0.#}% margin)"));
        return $"Ranked by profit this month: {ranked}.";
    }

    private async Task<string> AnswerTopExpenseCategories(Guid? branchId, DateOnly from, DateOnly to, string currency, CancellationToken ct)
    {
        var report = await mediator.Send(new GetExpenseReportQuery(from, to, branchId, null), ct);
        if (report.ByCategory.Count == 0)
        {
            return "No expenses recorded this month.";
        }
        var top = report.ByCategory.OrderByDescending(c => c.Amount).Take(3)
            .Select(c => $"{c.CategoryName} ({Money(c.Amount, currency)}, {c.PercentOfTotal:0.#}%)");
        return $"Your biggest expense categories this month are: {string.Join(", ", top)}. " +
               $"Total expenses so far this month: {Money(report.TotalAmount, currency)}.";
    }

    private async Task<string> AnswerAmIProfitable(Guid? branchId, DateOnly from, DateOnly to, string currency, CancellationToken ct)
    {
        var report = await mediator.Send(new GetProfitabilityReportQuery(from, to, branchId), ct);
        return report.Totals.NetProfit >= 0
            ? $"Yes - you're profitable this month. Net profit after expenses is {Money(report.Totals.NetProfit, currency)}."
            : $"Not yet this month - you're at a net loss of {Money(Math.Abs(report.Totals.NetProfit), currency)} after expenses.";
    }

    private static string Money(decimal amount, string currencyCode) => $"{currencyCode} {amount:N2}";
}
