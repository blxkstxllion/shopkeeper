namespace ShopKeeper.Application.Inventory.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Dtos;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Inventory.Dtos;
using ShopKeeper.Domain.Constants;

public record GetInventoryTransactionsQuery(Guid? ProductId, Guid? BranchId, int Page, int PageSize)
    : IRequest<PagedResult<InventoryTransactionDto>>;

public class GetInventoryTransactionsQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetInventoryTransactionsQuery, PagedResult<InventoryTransactionDto>>
{
    public async Task<PagedResult<InventoryTransactionDto>> Handle(GetInventoryTransactionsQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.InventoryView);
        if (request.BranchId.HasValue)
        {
            currentUser.RequireBranchAccess(request.BranchId.Value);
        }

        // A branch-restricted user (Cashier, Branch Manager) with no explicit filter should only
        // ever see their own branch's ledger, not every branch in the business.
        var effectiveBranchId = request.BranchId ?? currentUser.BranchId;

        var query =
            from t in db.InventoryTransactions
            join product in db.Products on t.ProductId equals product.Id
            join branch in db.Branches on t.BranchId equals branch.Id
            join user in db.Users on t.CreatedByUserId equals user.Id
            select new { t, product.Name, BranchName = branch.Name, user.FirstName, user.LastName };

        if (request.ProductId.HasValue)
        {
            query = query.Where(x => x.t.ProductId == request.ProductId);
        }

        if (effectiveBranchId.HasValue)
        {
            query = query.Where(x => x.t.BranchId == effectiveBranchId);
        }

        query = query.OrderByDescending(x => x.t.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var items = rows.Select(x => new InventoryTransactionDto(
            x.t.Id, x.t.ProductId, x.Name, x.t.BranchId, x.BranchName, x.t.Type.ToString(),
            x.t.QuantityChange, x.t.QuantityAfter, x.t.Reason, x.t.ReferenceType, x.t.ReferenceId,
            $"{x.FirstName} {x.LastName}", x.t.CreatedAt)).ToList();

        return new PagedResult<InventoryTransactionDto>(items, totalCount, page, pageSize);
    }
}
