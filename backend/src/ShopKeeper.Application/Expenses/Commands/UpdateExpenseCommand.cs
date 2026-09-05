namespace ShopKeeper.Application.Expenses.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

public record UpdateExpenseCommand(
    Guid Id,
    Guid? BranchId,
    Guid ExpenseCategoryId,
    decimal Amount,
    DateOnly ExpenseDate,
    string? Description,
    Guid? ClientRequestId = null) : IRequest, ISupportsClientRequestId;

public class UpdateExpenseCommandValidator : AbstractValidator<UpdateExpenseCommand>
{
    public UpdateExpenseCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.ExpenseCategoryId).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class UpdateExpenseCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<UpdateExpenseCommand>
{
    public async Task Handle(UpdateExpenseCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.ExpensesManage);
        if (request.BranchId.HasValue)
        {
            currentUser.RequireBranchAccess(request.BranchId.Value);
        }

        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Expense), request.Id);

        var categoryExists = await db.ExpenseCategories.AnyAsync(c => c.Id == request.ExpenseCategoryId, cancellationToken);
        if (!categoryExists)
        {
            throw new NotFoundException(nameof(ExpenseCategory), request.ExpenseCategoryId);
        }

        expense.BranchId = request.BranchId;
        expense.ExpenseCategoryId = request.ExpenseCategoryId;
        expense.Amount = request.Amount;
        expense.ExpenseDate = request.ExpenseDate;
        expense.Description = request.Description;

        await db.SaveChangesAsync(cancellationToken);
    }
}
