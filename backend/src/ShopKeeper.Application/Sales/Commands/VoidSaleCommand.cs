namespace ShopKeeper.Application.Sales.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

/// <summary>Fully reverses a sale's stock and revenue effect. Only valid while the sale is
/// still in its original Completed state - a partially/fully refunded sale must be corrected
/// via further refunds, not voided.</summary>
public record VoidSaleCommand(Guid SaleId, string Reason) : IRequest;

public class VoidSaleCommandValidator : AbstractValidator<VoidSaleCommand>
{
    public VoidSaleCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}

public class VoidSaleCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<VoidSaleCommand>
{
    public async Task Handle(VoidSaleCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.SalesVoid);
        var businessId = currentUser.RequireBusinessId();
        var userId = currentUser.RequireUserId();

        var sale = await db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Sale), request.SaleId);

        currentUser.RequireBranchAccess(sale.BranchId);

        if (sale.Status != SaleStatus.Completed)
        {
            throw new ConflictException($"Sale {sale.SaleNumber} cannot be voided from its current status ({sale.Status}).");
        }

        foreach (var item in sale.Items)
        {
            var stock = await db.ProductStocks.FirstOrDefaultAsync(
                s => s.ProductId == item.ProductId && s.BranchId == sale.BranchId, cancellationToken);

            if (stock is null)
            {
                continue; // product no longer tracks inventory - nothing to restore
            }

            var newQuantity = stock.QuantityOnHand + item.Quantity;
            stock.QuantityOnHand = newQuantity;

            db.InventoryTransactions.Add(new InventoryTransaction
            {
                BusinessId = businessId,
                ProductId = item.ProductId,
                BranchId = sale.BranchId,
                Type = InventoryTransactionType.Refund,
                QuantityChange = item.Quantity,
                QuantityAfter = newQuantity,
                Reason = $"Void of sale {sale.SaleNumber}",
                ReferenceType = "Sale",
                ReferenceId = sale.Id,
                CreatedByUserId = userId,
            });
        }

        sale.Status = SaleStatus.Voided;
        sale.VoidedAt = DateTimeOffset.UtcNow;
        sale.VoidedByUserId = userId;
        sale.VoidReason = request.Reason;

        await db.SaveChangesAsync(cancellationToken);
    }
}
