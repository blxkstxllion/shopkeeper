namespace ShopKeeper.Application.Reports.Queries;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Reports.Dtos;
using ShopKeeper.Domain.Constants;

public record GetExpenseReportQuery(DateOnly From, DateOnly To, Guid? BranchId, Guid? CategoryId)
    : IRequest<ExpenseReportDto>, IRequirePlanFeature
{
    public bool RequiresReports => true;
    public bool RequiresAi => false;
    public bool RequiresCustomRoles => false;
}

public class GetExpenseReportQueryValidator : AbstractValidator<GetExpenseReportQuery>
{
    public GetExpenseReportQueryValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("'To' must not be before 'From'.");
    }
}

public class GetExpenseReportQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetExpenseReportQuery, ExpenseReportDto>
{
    public async Task<ExpenseReportDto> Handle(GetExpenseReportQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.ReportsView);
        if (request.BranchId.HasValue)
        {
            currentUser.RequireBranchAccess(request.BranchId.Value);
        }

        var branchId = request.BranchId ?? currentUser.BranchId;

        var query = db.Expenses
            .Include(e => e.ExpenseCategory)
            .Where(e => e.IsActive && e.ExpenseDate >= request.From && e.ExpenseDate <= request.To);

        if (branchId.HasValue)
        {
            query = query.Where(e => e.BranchId == branchId);
        }
        if (request.CategoryId.HasValue)
        {
            query = query.Where(e => e.ExpenseCategoryId == request.CategoryId);
        }

        var expenses = await query.ToListAsync(cancellationToken);

        var totalAmount = expenses.Sum(e => e.Amount);

        var byCategory = expenses
            .GroupBy(e => e.ExpenseCategory.Name)
            .Select(g => new ExpenseCategoryTotalDto(
                g.Key, g.Sum(e => e.Amount), totalAmount > 0 ? Math.Round(g.Sum(e => e.Amount) / totalAmount * 100, 1) : 0))
            .OrderByDescending(c => c.Amount)
            .ToList();

        var dailyTrend = new List<DailyExpensePointDto>();
        for (var day = request.From; day <= request.To; day = day.AddDays(1))
        {
            dailyTrend.Add(new DailyExpensePointDto(day, expenses.Where(e => e.ExpenseDate == day).Sum(e => e.Amount)));
        }

        return new ExpenseReportDto(totalAmount, byCategory, dailyTrend);
    }
}
