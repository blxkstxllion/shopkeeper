namespace ShopKeeper.Application.Products.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Products.Dtos;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

public record GetProductByIdQuery(Guid Id, Guid? BranchId) : IRequest<ProductDto>;

public class GetProductByIdQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetProductByIdQuery, ProductDto>
{
    public async Task<ProductDto> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.InventoryView);
        var branchId = request.BranchId ?? currentUser.BranchId;

        var product = await db.Products.Include(p => p.Category).Include(p => p.Supplier).FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.Id);

        int? quantityOnHand = branchId.HasValue
            ? await db.ProductStocks.Where(s => s.ProductId == product.Id && s.BranchId == branchId).Select(s => (int?)s.QuantityOnHand).FirstOrDefaultAsync(cancellationToken)
            : null;

        return new ProductDto(
            product.Id, product.Name, product.Sku, product.Barcode, product.Description, product.ImageUrl,
            product.CategoryId, product.Category?.Name, product.SupplierId, product.Supplier?.Name,
            product.SellingPrice, product.CostPrice, product.MinimumStock,
            product.TrackInventory, product.IsActive, quantityOnHand,
            quantityOnHand.HasValue && quantityOnHand.Value <= product.MinimumStock);
    }
}
