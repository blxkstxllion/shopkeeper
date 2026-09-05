namespace ShopKeeper.Application.Businesses.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

/// <summary>Soft delete only (IsActive = false) - a branch's Sales/InventoryTransactions/ProductStocks
/// must remain intact for historical reporting. See section 40: avoid deleting financial records.</summary>
public record DeleteBranchCommand(Guid Id, Guid? ClientRequestId = null) : IRequest, ISupportsClientRequestId;

public class DeleteBranchCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<DeleteBranchCommand>
{
    public async Task Handle(DeleteBranchCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.BranchesManage);

        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Branch), request.Id);

        if (branch.IsMainBranch)
        {
            throw new ConflictException("Can't deactivate the main branch. Promote another branch to main first.");
        }

        var otherActiveCount = await db.Branches.CountAsync(b => b.Id != request.Id && b.IsActive, cancellationToken);
        if (otherActiveCount == 0)
        {
            throw new ConflictException("Can't deactivate the only active branch.");
        }

        branch.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
    }
}
