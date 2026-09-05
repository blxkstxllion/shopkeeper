namespace ShopKeeper.Application.Expenses.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Expenses.Dtos;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

public record CreateExpenseCommand(
    Guid? BranchId,
    Guid ExpenseCategoryId,
    decimal Amount,
    DateOnly ExpenseDate,
    string? Description,
    Guid? ClientRequestId = null) : IRequest<ExpenseDto>, ISupportsClientRequestId;

public class CreateExpenseCommandValidator : AbstractValidator<CreateExpenseCommand>
{
    public CreateExpenseCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.ExpenseCategoryId).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class CreateExpenseCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateExpenseCommand, ExpenseDto>
{
    public async Task<ExpenseDto> Handle(CreateExpenseCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.ExpensesManage);
        if (request.BranchId.HasValue)
        {
            currentUser.RequireBranchAccess(request.BranchId.Value);
        }
        var businessId = currentUser.RequireBusinessId();

        var category = await db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == request.ExpenseCategoryId, cancellationToken)
            ?? throw new NotFoundException(nameof(ExpenseCategory), request.ExpenseCategoryId);

        var expense = new Expense
        {
            BusinessId = businessId,
            BranchId = request.BranchId,
            ExpenseCategoryId = request.ExpenseCategoryId,
            Amount = request.Amount,
            ExpenseDate = request.ExpenseDate,
            Description = request.Description,
            CreatedByUserId = currentUser.RequireUserId(),
        };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync(cancellationToken);

        var branchName = request.BranchId.HasValue
            ? await db.Branches.Where(b => b.Id == request.BranchId).Select(b => b.Name).FirstOrDefaultAsync(cancellationToken)
            : null;
        var createdByName = await db.Users.Where(u => u.Id == expense.CreatedByUserId)
            .Select(u => u.FirstName + " " + u.LastName).FirstAsync(cancellationToken);

        return new ExpenseDto(
            expense.Id, expense.BranchId, branchName, expense.ExpenseCategoryId, category.Name,
            expense.Amount, expense.ExpenseDate, expense.Description, createdByName, expense.CreatedAt);
    }
}
