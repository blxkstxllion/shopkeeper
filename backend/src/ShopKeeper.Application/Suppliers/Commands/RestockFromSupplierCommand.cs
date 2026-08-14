namespace ShopKeeper.Application.Suppliers.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Inventory.Commands;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

/// <summary>
/// A restock is just a stock increase tagged with which supplier it came from - reuses
/// AdjustStockCommand for the actual ledger/quantity logic (never duplicate the "stock can't
/// go negative, always produces an InventoryTransaction" invariant) rather than a parallel
/// PurchaseOrder subsystem this app doesn't otherwise have a use for yet. If UnitCost is given
/// and differs from the product's current cost, updates it too - a restock is the natural
/// moment a per-unit cost actually changes.
/// </summary>
public record RestockFromSupplierCommand(Guid SupplierId, Guid ProductId, Guid BranchId, int Quantity, decimal? UnitCost) : IRequest<int>;

public class RestockFromSupplierCommandValidator : AbstractValidator<RestockFromSupplierCommand>
{
    public RestockFromSupplierCommandValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).When(x => x.UnitCost.HasValue);
    }
}

public class RestockFromSupplierCommandHandler(IAppDbContext db, ICurrentUserService currentUser, ISender mediator)
    : IRequestHandler<RestockFromSupplierCommand, int>
{
    public async Task<int> Handle(RestockFromSupplierCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.SuppliersManage);

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken)
            ?? throw new NotFoundException(nameof(Supplier), request.SupplierId);

        if (!supplier.IsActive)
        {
            throw new ConflictException("This supplier is inactive.");
        }

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        var newQuantity = await mediator.Send(
            new AdjustStockCommand(request.ProductId, request.BranchId, request.Quantity, $"Restock from {supplier.Name}", "Supplier", request.SupplierId),
            cancellationToken);

        if (request.UnitCost.HasValue && request.UnitCost.Value != product.CostPrice)
        {
            product.CostPrice = request.UnitCost.Value;
            await db.SaveChangesAsync(cancellationToken);
        }

        return newQuantity;
    }
}
