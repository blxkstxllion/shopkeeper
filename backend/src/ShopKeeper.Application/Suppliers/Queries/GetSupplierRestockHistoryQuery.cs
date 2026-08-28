namespace ShopKeeper.Application.Suppliers.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Suppliers.Dtos;

public record GetSupplierRestockHistoryQuery(Guid SupplierId) : IRequest<IReadOnlyList<SupplierRestockDto>>;

/// <summary>
/// Filters InventoryTransaction by the ReferenceType/ReferenceId tag RestockFromSupplierCommand
/// stamps on each restock - a supplier's history is just a slice of the existing ledger, not a
/// separate table. Ordering happens in memory after the fetch, not via ORDER BY in the query:
/// the SQLite provider (test suite only) can't translate DateTimeOffset in ORDER BY, the same
/// constraint documented on GetDashboardSummaryQuery.
/// </summary>
public class GetSupplierRestockHistoryQueryHandler(IAppDbContext db)
    : IRequestHandler<GetSupplierRestockHistoryQuery, IReadOnlyList<SupplierRestockDto>>
{
    public async Task<IReadOnlyList<SupplierRestockDto>> Handle(GetSupplierRestockHistoryQuery request, CancellationToken cancellationToken)
    {
        var rows = await db.InventoryTransactions
            .Include(t => t.Product)
            .Include(t => t.Branch)
            .Where(t => t.ReferenceType == "Supplier" && t.ReferenceId == request.SupplierId)
            .Select(t => new
            {
                t.Id,
                t.ProductId,
                ProductName = t.Product.Name,
                t.BranchId,
                BranchName = t.Branch.Name,
                t.QuantityChange,
                t.CreatedByUserId,
                t.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var userNames = await db.Users
            .Where(u => rows.Select(r => r.CreatedByUserId).Contains(u.Id))
            .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName })
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        return rows
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new SupplierRestockDto(
                r.Id, r.ProductId, r.ProductName, r.BranchId, r.BranchName,
                r.QuantityChange, userNames.GetValueOrDefault(r.CreatedByUserId, "Unknown"), r.CreatedAt))
            .ToList();
    }
}
