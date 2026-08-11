namespace ShopKeeper.Application.Expenses.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Expenses.Dtos;

public record GetExpenseCategoriesQuery : IRequest<IReadOnlyList<ExpenseCategoryDto>>;

public class GetExpenseCategoriesQueryHandler(IAppDbContext db)
    : IRequestHandler<GetExpenseCategoriesQuery, IReadOnlyList<ExpenseCategoryDto>>
{
    public async Task<IReadOnlyList<ExpenseCategoryDto>> Handle(GetExpenseCategoriesQuery request, CancellationToken cancellationToken) =>
        await db.ExpenseCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new ExpenseCategoryDto(c.Id, c.Name, c.Description, c.IsActive))
            .ToListAsync(cancellationToken);
}
