namespace ShopKeeper.Application.Inventory.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

/// <summary>
/// The only way stock quantities change outside of a sale/refund. Always produces an
/// InventoryTransaction - see section 12/40 of the product spec: "Never silently change
/// stock quantities."
/// </summary>
public record AdjustStockCommand(
    Guid ProductId,
    Guid BranchId,
    int QuantityChange,
    string Reason,
    string? ReferenceType = null,
    Guid? ReferenceId = null) : IRequest<int>;

public class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(x => x.QuantityChange).NotEqual(0).WithMessage("Quantity change cannot be zero.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

/// <summary>Returns the resulting quantity on hand.</summary>
public class AdjustStockCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<AdjustStockCommand, int>
{
    public async Task<int> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.InventoryModify);
        currentUser.RequireBranchAccess(request.BranchId);
        var businessId = currentUser.RequireBusinessId();

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        if (!product.TrackInventory)
        {
            throw new ConflictException($"'{product.Name}' does not track inventory.");
        }

        var stock = await db.ProductStocks.FirstOrDefaultAsync(
            s => s.ProductId == request.ProductId && s.BranchId == request.BranchId, cancellationToken);

        if (stock is null)
        {
            stock = new ProductStock { BusinessId = businessId, ProductId = request.ProductId, BranchId = request.BranchId, QuantityOnHand = 0 };
            db.ProductStocks.Add(stock);
        }

        var newQuantity = stock.QuantityOnHand + request.QuantityChange;
        if (newQuantity < 0)
        {
            throw new ConflictException(
                $"This adjustment would take '{product.Name}' to {newQuantity} units. Current stock is {stock.QuantityOnHand}.");
        }

        stock.QuantityOnHand = newQuantity;

        db.InventoryTransactions.Add(new InventoryTransaction
        {
            BusinessId = businessId,
            ProductId = request.ProductId,
            BranchId = request.BranchId,
            Type = InventoryTransactionType.Adjustment,
            QuantityChange = request.QuantityChange,
            QuantityAfter = newQuantity,
            Reason = request.Reason,
            ReferenceType = request.ReferenceType,
            ReferenceId = request.ReferenceId,
            CreatedByUserId = currentUser.RequireUserId(),
        });

        await db.SaveChangesAsync(cancellationToken);
        return newQuantity;
    }
}
