namespace ShopKeeper.Application.Expenses.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

/// <summary>Soft delete only - financial records are voided, never hard-deleted. See section 40.</summary>
public record DeleteExpenseCommand(Guid Id, Guid? ClientRequestId = null) : IRequest, ISupportsClientRequestId;

public class DeleteExpenseCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<DeleteExpenseCommand>
{
    public async Task Handle(DeleteExpenseCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.ExpensesManage);

        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Expense), request.Id);

        if (expense.BranchId.HasValue)
        {
            currentUser.RequireBranchAccess(expense.BranchId.Value);
        }

        expense.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
    }
}
