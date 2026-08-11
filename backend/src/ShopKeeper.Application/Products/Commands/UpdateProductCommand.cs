namespace ShopKeeper.Application.Products.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

public record UpdateProductCommand(
    Guid Id,
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
    bool IsActive) : IRequest;

public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Barcode).MaximumLength(50);
        RuleFor(x => x.SellingPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0);
    }
}

public class UpdateProductCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<UpdateProductCommand>
{
    public async Task Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.ProductsManage);

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.Id);

        var skuTaken = await db.Products.AnyAsync(p => p.Id != request.Id && p.Sku == request.Sku, cancellationToken);
        if (skuTaken)
        {
            throw new ConflictException($"A product with SKU '{request.Sku}' already exists.");
        }

        product.Name = request.Name.Trim();
        product.Sku = request.Sku.Trim();
        product.Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();
        product.Description = request.Description;
        product.ImageUrl = request.ImageUrl;
        product.CategoryId = request.CategoryId;
        product.SellingPrice = request.SellingPrice;
        product.CostPrice = request.CostPrice;
        product.MinStock = request.MinStock;
        product.ReorderLevel = request.ReorderLevel;
        product.TrackInventory = request.TrackInventory;
        product.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
