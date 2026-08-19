namespace ShopKeeper.Application.Reports.Queries;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Reports.Dtos;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Enums;

public record GetProfitabilityReportQuery(DateOnly From, DateOnly To, Guid? BranchId)
    : IRequest<ProfitabilityReportDto>, IRequirePlanFeature
{
    public bool RequiresReports => true;
    public bool RequiresAi => false;
    public bool RequiresCustomRoles => false;
}

public class GetProfitabilityReportQueryValidator : AbstractValidator<GetProfitabilityReportQuery>
{
    public GetProfitabilityReportQueryValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("'To' must not be before 'From'.");
    }
}

/// <summary>
/// Status and branch are filtered in the database query; the date range is filtered afterward in
/// memory. This is not a stylistic choice: confirmed by direct testing (not assumption) that EF
/// Core's SQLite provider (test suite only) cannot translate ANY WHERE-clause comparison on
/// Sale.CreatedAt (a DateTimeOffset/timestamptz column) - not "only when combined with Include,"
/// not "only with certain operators," it fails standalone, with no other filter present. Fixing
/// that for real would mean either switching the test suite off SQLite for this query (against
/// this repo's stated preference for a real SQLite database over mocking) or reaching for
/// parameterized raw SQL as an explicit escape hatch - neither undertaken here without a decision
/// from whoever owns that tradeoff. What IS filtered server-side now (status, branch) still cuts
/// materially into what previously came back as "every sale this branch has ever made." Expenses
/// ARE netted out of GrossProfit here (NetProfit = GrossProfit - Expenses), unlike the Dashboard
/// which doesn't show expenses at all yet.
/// Refunds are not netted out of revenue/profit, consistent with the Dashboard's existing
/// behavior - both surfaces should agree, not silently diverge on how a refunded sale counts.
/// </summary>
public class GetProfitabilityReportQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetProfitabilityReportQuery, ProfitabilityReportDto>
{
    public async Task<ProfitabilityReportDto> Handle(GetProfitabilityReportQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.ReportsView);
        if (request.BranchId.HasValue)
        {
            currentUser.RequireBranchAccess(request.BranchId.Value);
        }

        var branchId = request.BranchId ?? currentUser.BranchId;
        var isUnscoped = !branchId.HasValue;

        var rangeStart = new DateTimeOffset(request.From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var rangeEndExclusive = new DateTimeOffset(request.To.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(1);

        var salesQuery = db.Sales
            .Include(s => s.CashierUser)
            .Include(s => s.Branch)
            .Include(s => s.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.Category)
            .Where(s => s.Status != SaleStatus.Voided);

        if (branchId.HasValue)
        {
            salesQuery = salesQuery.Where(s => s.BranchId == branchId);
        }

        var allMatchingSales = await salesQuery.ToListAsync(cancellationToken);
        var sales = allMatchingSales.Where(s => s.CreatedAt >= rangeStart && s.CreatedAt < rangeEndExclusive).ToList();

        var expensesQuery = db.Expenses.Where(e => e.IsActive && e.ExpenseDate >= request.From && e.ExpenseDate <= request.To);
        if (branchId.HasValue)
        {
            expensesQuery = expensesQuery.Where(e => e.BranchId == branchId);
        }
        var expenses = await expensesQuery.ToListAsync(cancellationToken);

        var revenue = sales.Sum(s => s.Subtotal - s.DiscountAmount);
        var cogs = sales.Sum(s => s.TotalCost);
        var grossProfit = sales.Sum(s => s.GrossProfit);
        var totalExpenses = expenses.Sum(e => e.Amount);
        var netProfit = grossProfit - totalExpenses;

        var totals = new ProfitabilityTotalsDto(
            revenue, cogs, grossProfit,
            revenue > 0 ? Math.Round(grossProfit / revenue * 100, 1) : 0,
            totalExpenses, netProfit,
            revenue > 0 ? Math.Round(netProfit / revenue * 100, 1) : 0);

        var dailyTrend = new List<DailyProfitPointDto>();
        for (var day = request.From; day <= request.To; day = day.AddDays(1))
        {
            var daySales = sales.Where(s => DateOnly.FromDateTime(s.CreatedAt.Date) == day).ToList();
            var dayExpenseTotal = expenses.Where(e => e.ExpenseDate == day).Sum(e => e.Amount);
            var dayRevenue = daySales.Sum(s => s.Subtotal - s.DiscountAmount);
            var dayNetProfit = daySales.Sum(s => s.GrossProfit) - dayExpenseTotal;
            dailyTrend.Add(new DailyProfitPointDto(day, dayRevenue, dayNetProfit));
        }

        var byCategory = sales
            .SelectMany(s => s.Items)
            .GroupBy(i => i.Product?.Category?.Name ?? "Uncategorized")
            .Select(g => new CategoryProfitDto(g.Key, g.Sum(i => i.LineRevenue), g.Sum(i => i.LineProfit)))
            .OrderByDescending(c => c.Profit)
            .ToList();

        var byBranch = isUnscoped
            ? sales
                .GroupBy(s => new { s.BranchId, BranchName = s.Branch.Name })
                .Select(g =>
                {
                    var branchRevenue = g.Sum(s => s.Subtotal - s.DiscountAmount);
                    var branchProfit = g.Sum(s => s.GrossProfit);
                    return new BranchProfitDto(
                        g.Key.BranchId, g.Key.BranchName, branchRevenue, branchProfit,
                        branchRevenue > 0 ? Math.Round(branchProfit / branchRevenue * 100, 1) : 0);
                })
                .OrderByDescending(b => b.Profit)
                .ToList()
            : [];

        var byCashier = sales
            .GroupBy(s => new { s.CashierUserId, CashierName = s.CashierUser.FirstName + " " + s.CashierUser.LastName })
            .Select(g => new CashierProfitDto(g.Key.CashierUserId, g.Key.CashierName, g.Sum(s => s.Subtotal - s.DiscountAmount), g.Sum(s => s.GrossProfit), g.Count()))
            .OrderByDescending(c => c.Profit)
            .ToList();

        var productProfits = sales
            .SelectMany(s => s.Items)
            .GroupBy(i => i.ProductNameSnapshot)
            .Select(g => new ProductProfitDto(g.Key, g.Sum(i => i.LineRevenue), g.Sum(i => i.LineProfit), g.Sum(i => i.Quantity)))
            .ToList();

        var topProducts = productProfits.OrderByDescending(p => p.Profit).Take(5).ToList();
        var worstProducts = productProfits.OrderBy(p => p.Profit).Take(5).ToList();

        return new ProfitabilityReportDto(totals, dailyTrend, byCategory, byBranch, byCashier, topProducts, worstProducts);
    }
}
