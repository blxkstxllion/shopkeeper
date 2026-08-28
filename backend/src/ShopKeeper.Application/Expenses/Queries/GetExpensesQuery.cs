namespace ShopKeeper.Application.Expenses.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Dtos;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Expenses.Dtos;
using ShopKeeper.Domain.Constants;

public record GetExpensesQuery(
    DateOnly? From,
    DateOnly? To,
    Guid? CategoryId,
    Guid? BranchId,
    int Page,
    int PageSize) : IRequest<PagedResult<ExpenseDto>>;

public class GetExpensesQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetExpensesQuery, PagedResult<ExpenseDto>>
{
    public async Task<PagedResult<ExpenseDto>> Handle(GetExpensesQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.ExpensesView);
        if (request.BranchId.HasValue)
        {
            currentUser.RequireBranchAccess(request.BranchId.Value);
        }

        var branchId = request.BranchId ?? currentUser.BranchId;

        var query = db.Expenses.Where(e => e.IsActive);

        if (branchId.HasValue)
        {
            query = query.Where(e => e.BranchId == branchId);
        }
        if (request.From.HasValue)
        {
            query = query.Where(e => e.ExpenseDate >= request.From.Value);
        }
        if (request.To.HasValue)
        {
            query = query.Where(e => e.ExpenseDate <= request.To.Value);
        }
        if (request.CategoryId.HasValue)
        {
            query = query.Where(e => e.ExpenseCategoryId == request.CategoryId);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        // ThenBy CreatedAt (DateTimeOffset) intentionally avoided: the SQLite provider (used only by
        // this project's test suite) can't translate DateTimeOffset in ORDER BY - see
        // GetDashboardSummaryQuery's doc comment for the same constraint. Id is a stable, translatable
        // tie-breaker for same-day expenses; exact insertion order isn't a requirement here.
        var rows = await query
            .OrderByDescending(e => e.ExpenseDate)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                e.BranchId,
                BranchName = e.Branch != null ? e.Branch.Name : null,
                e.ExpenseCategoryId,
                CategoryName = e.ExpenseCategory.Name,
                e.Amount,
                e.ExpenseDate,
                e.Description,
                CreatedByName = e.CreatedByUser.FirstName + " " + e.CreatedByUser.LastName,
                e.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new ExpenseDto(
            r.Id, r.BranchId, r.BranchName, r.ExpenseCategoryId, r.CategoryName,
            r.Amount, r.ExpenseDate, r.Description, r.CreatedByName, r.CreatedAt)).ToList();

        return new PagedResult<ExpenseDto>(items, totalCount, page, pageSize);
    }
}
