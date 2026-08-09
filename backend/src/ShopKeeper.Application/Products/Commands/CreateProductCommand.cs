namespace ShopKeeper.Application.Products.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Products.Dtos;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

public record CreateProductCommand(
    string Name,
    string Sku,
    string? Barcode,
    string? Description,
    string? ImageUrl,
    Guid? CategoryId,
    decimal SellingPrice,
    decimal CostPrice,
    int MinStock,
    int ReorderLevel,
    bool TrackInventory,
    int InitialQuantity,
    Guid? BranchId) : IRequest<ProductDto>;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Barcode).MaximumLength(50);
        RuleFor(x => x.SellingPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0);
        RuleFor(x => x.InitialQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.BranchId).NotNull().When(x => x.TrackInventory && x.InitialQuantity > 0)
            .WithMessage("A branch is required to receive the initial stock quantity.");
    }
}

public class CreateProductCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.ProductsManage);
        var businessId = currentUser.RequireBusinessId();

        var skuTaken = await db.Products.AnyAsync(p => p.Sku == request.Sku, cancellationToken);
        if (skuTaken)
        {
            throw new ConflictException($"A product with SKU '{request.Sku}' already exists.");
        }

        var product = new Product
        {
            BusinessId = businessId,
            Name = request.Name.Trim(),
            Sku = request.Sku.Trim(),
            Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim(),
            Description = request.Description,
            ImageUrl = request.ImageUrl,
            CategoryId = request.CategoryId,
            SellingPrice = request.SellingPrice,
            CostPrice = request.CostPrice,
            MinStock = request.MinStock,
            ReorderLevel = request.ReorderLevel,
            TrackInventory = request.TrackInventory,
        };
        db.Products.Add(product);

        int? quantityOnHand = null;
        if (request.TrackInventory && request.BranchId.HasValue)
        {
            quantityOnHand = request.InitialQuantity;

            db.ProductStocks.Add(new ProductStock
            {
                BusinessId = businessId,
                Product = product,
                BranchId = request.BranchId.Value,
                QuantityOnHand = request.InitialQuantity,
            });

            if (request.InitialQuantity > 0)
            {
                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    BusinessId = businessId,
                    Product = product,
                    BranchId = request.BranchId.Value,
                    Type = InventoryTransactionType.InitialStock,
                    QuantityChange = request.InitialQuantity,
                    QuantityAfter = request.InitialQuantity,
                    Reason = "Initial stock on product creation",
                    CreatedByUserId = currentUser.RequireUserId(),
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var categoryName = request.CategoryId.HasValue
            ? await db.ProductCategories.Where(c => c.Id == request.CategoryId).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken)
            : null;

        return new ProductDto(
            product.Id, product.Name, product.Sku, product.Barcode, product.Description, product.ImageUrl,
            product.CategoryId, categoryName, product.SellingPrice, product.CostPrice, product.MinStock,
            product.ReorderLevel, product.TrackInventory, product.IsActive, quantityOnHand,
            quantityOnHand.HasValue && quantityOnHand.Value <= product.ReorderLevel);
    }
}
