namespace ShopKeeper.Application.Inventory.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Inventory.Dtos;
using ShopKeeper.Domain.Constants;

public record GetInventoryStatsQuery(Guid? BranchId) : IRequest<InventoryStatsDto>;

/// <summary>
/// Deliberately independent of Sales/GetDashboardSummaryQuery so an Inventory Manager
/// (who has inventory:view but not sales:view) can load the Inventory page's stats row
/// without a 403 - see DefaultRoles.InventoryManager.
/// </summary>
public class GetInventoryStatsQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetInventoryStatsQuery, InventoryStatsDto>
{
    public async Task<InventoryStatsDto> Handle(GetInventoryStatsQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.InventoryView);
        if (request.BranchId.HasValue)
        {
            currentUser.RequireBranchAccess(request.BranchId.Value);
        }

        var branchId = request.BranchId ?? currentUser.BranchId;

        var stockQuery = db.ProductStocks.Include(ps => ps.Product).Where(ps => ps.Product.IsActive);
        if (branchId.HasValue)
        {
            stockQuery = stockQuery.Where(ps => ps.BranchId == branchId);
        }

        var stocks = await stockQuery.ToListAsync(cancellationToken);

        return new InventoryStatsDto(
            TotalProducts: stocks.Count,
            LowStockCount: stocks.Count(s => s.QuantityOnHand > 0 && s.QuantityOnHand <= s.Product.MinimumStock),
            OutOfStockCount: stocks.Count(s => s.QuantityOnHand == 0),
            InventoryValue: stocks.Sum(s => s.QuantityOnHand * s.Product.CostPrice));
    }
}
