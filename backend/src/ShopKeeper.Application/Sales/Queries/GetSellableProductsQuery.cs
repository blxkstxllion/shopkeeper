namespace ShopKeeper.Application.Sales.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Sales.Dtos;
using ShopKeeper.Domain.Constants;

/// <summary>Powers the POS product grid: active products with their stock at the given branch.</summary>
public record GetSellableProductsQuery(Guid BranchId, string? Search, Guid? CategoryId) : IRequest<IReadOnlyList<SellableProductDto>>;

public class GetSellableProductsQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetSellableProductsQuery, IReadOnlyList<SellableProductDto>>
{
    public async Task<IReadOnlyList<SellableProductDto>> Handle(GetSellableProductsQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.SalesCreate);

        var query = db.Products.Where(p => p.IsActive);

        if (request.CategoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == request.CategoryId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term)
                || p.Sku.ToLower().Contains(term)
                || (p.Barcode != null && p.Barcode.ToLower().Contains(term)));
        }

        return await query
            .OrderBy(p => p.Name)
            .Select(p => new SellableProductDto(
                p.Id, p.Name, p.Sku, p.Barcode, p.ImageUrl, p.CategoryId, p.SellingPrice, p.TrackInventory,
                p.TrackInventory
                    ? p.StockByBranch.Where(s => s.BranchId == request.BranchId).Select(s => (int?)s.QuantityOnHand).FirstOrDefault()
                    : null))
            .ToListAsync(cancellationToken);
    }
}
