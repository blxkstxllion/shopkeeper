namespace ShopKeeper.Application.Advisor.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Advisor.Dtos;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Reports.Queries;
using ShopKeeper.Application.Dashboard.Queries;
using ShopKeeper.Domain.Constants;

public record GetAdvisorAnswerQuery(AdvisorQuestionId QuestionId, Guid? BranchId) : IRequest<AdvisorAnswerDto>;

/// <summary>
/// Answers a fixed set of business questions by calling the same MediatR queries
/// Dashboard/Reports already use and formatting their DTOs into a sentence - no separate
/// aggregation logic, so the advisor's numbers can never disagree with what those pages show.
/// This is deliberately not an LLM: the question set is closed (AdvisorQuestionId), so "answering"
/// means routing to a known calculation, not interpreting free text. If a real conversational
/// layer is added later, these same calculations become the tools an LLM would call - this
/// handler is the foundation for that, not a placeholder to be thrown away.
/// </summary>
public class GetAdvisorAnswerQueryHandler(ISender mediator, IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetAdvisorAnswerQuery, AdvisorAnswerDto>
{
    public async Task<AdvisorAnswerDto> Handle(GetAdvisorAnswerQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.AiConsultantUse);

        var businessId = currentUser.RequireBusinessId();
        var currencyCode = await db.Businesses.Where(b => b.Id == businessId).Select(b => b.CurrencyCode).FirstAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var answer = request.QuestionId switch
        {
            AdvisorQuestionId.RevenueThisMonth => await AnswerRevenueThisMonth(request.BranchId, currencyCode, cancellationToken),
            AdvisorQuestionId.ProfitMargin => await AnswerProfitMargin(request.BranchId, monthStart, today, currencyCode, cancellationToken),
            AdvisorQuestionId.LowStock => await AnswerLowStock(request.BranchId, monthStart, today, cancellationToken),
            AdvisorQuestionId.BestSellingProduct => await AnswerBestSellingProduct(request.BranchId, currencyCode, cancellationToken),
            AdvisorQuestionId.WorstPerformingProduct => await AnswerWorstPerformingProduct(request.BranchId, monthStart, today, currencyCode, cancellationToken),
            AdvisorQuestionId.BranchComparison => await AnswerBranchComparison(monthStart, today, currencyCode, cancellationToken),
            AdvisorQuestionId.TopExpenseCategories => await AnswerTopExpenseCategories(request.BranchId, monthStart, today, currencyCode, cancellationToken),
            AdvisorQuestionId.AmIProfitable => await AnswerAmIProfitable(request.BranchId, monthStart, today, currencyCode, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(request), $"Unknown question id '{request.QuestionId}'."),
        };

        return new AdvisorAnswerDto(answer, DateTimeOffset.UtcNow);
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
