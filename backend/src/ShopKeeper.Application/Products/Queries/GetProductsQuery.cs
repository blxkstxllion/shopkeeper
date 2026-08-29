namespace ShopKeeper.Application.Products.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Dtos;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Products.Dtos;
using ShopKeeper.Domain.Constants;

public record GetProductsQuery(
    string? Search,
    Guid? CategoryId,
    Guid? BranchId,
    bool ActiveOnly,
    bool LowStockOnly,
    int Page,
    int PageSize) : IRequest<PagedResult<ProductDto>>;

public class GetProductsQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetProductsQuery, PagedResult<ProductDto>>
{
    public async Task<PagedResult<ProductDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.InventoryView);
        if (request.BranchId.HasValue)
        {
            currentUser.RequireBranchAccess(request.BranchId.Value);
        }

        var branchId = request.BranchId ?? currentUser.BranchId;

        var query = db.Products.AsQueryable();

        if (request.ActiveOnly)
        {
            query = query.Where(p => p.IsActive);
        }

        if (request.CategoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == request.CategoryId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // .ToLower().Contains() rather than EF.Functions.ILike: the latter is Npgsql-only and
            // would fail translation under the SQLite provider this project's tests run against -
            // see CLAUDE.md on preferring a real (if different) database in tests over mocking.
            var term = request.Search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term)
                || p.Sku.ToLower().Contains(term)
                || (p.Barcode != null && p.Barcode.ToLower().Contains(term)));
        }

        var projected = query
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Sku,
                p.Barcode,
                p.Description,
                p.ImageUrl,
                p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : null,
                p.SupplierId,
                SupplierName = p.Supplier != null ? p.Supplier.Name : null,
                p.SellingPrice,
                p.CostPrice,
                p.MinimumStock,
                p.TrackInventory,
                p.IsActive,
                QuantityOnHand = branchId == null
                    ? (int?)null
                    : p.StockByBranch.Where(s => s.BranchId == branchId).Select(s => (int?)s.QuantityOnHand).FirstOrDefault(),
            });

        if (request.LowStockOnly)
        {
            projected = projected.Where(p => p.QuantityOnHand != null && p.QuantityOnHand <= p.MinimumStock);
        }

        var totalCount = await projected.CountAsync(cancellationToken);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var items = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(p => new ProductDto(
            p.Id, p.Name, p.Sku, p.Barcode, p.Description, p.ImageUrl, p.CategoryId, p.CategoryName,
            p.SupplierId, p.SupplierName,
            p.SellingPrice, p.CostPrice, p.MinimumStock, p.TrackInventory, p.IsActive,
            p.QuantityOnHand, p.QuantityOnHand.HasValue && p.QuantityOnHand.Value <= p.MinimumStock)).ToList();

        return new PagedResult<ProductDto>(dtos, totalCount, page, pageSize);
    }
}
