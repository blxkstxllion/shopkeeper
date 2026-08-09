namespace ShopKeeper.Application.Sales.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Sales.Dtos;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

public record RefundLineInput(Guid SaleItemId, int Quantity);

public record RefundSaleCommand(Guid SaleId, IReadOnlyList<RefundLineInput> Items, string Reason) : IRequest<RefundDto>;

public class RefundSaleCommandValidator : AbstractValidator<RefundSaleCommand>
{
    public RefundSaleCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Items).NotEmpty().WithMessage("A refund needs at least one item.");
        RuleForEach(x => x.Items).ChildRules(item => item.RuleFor(i => i.Quantity).GreaterThan(0));
    }
}

public class RefundSaleCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<RefundSaleCommand, RefundDto>
{
    public async Task<RefundDto> Handle(RefundSaleCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.SalesRefund);
        var businessId = currentUser.RequireBusinessId();
        var userId = currentUser.RequireUserId();

        var sale = await db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Sale), request.SaleId);

        if (sale.Status is not (SaleStatus.Completed or SaleStatus.PartiallyRefunded))
        {
            throw new ConflictException($"Sale {sale.SaleNumber} cannot be refunded from its current status ({sale.Status}).");
        }

        var itemsById = sale.Items.ToDictionary(i => i.Id);

        foreach (var line in request.Items)
        {
            if (!itemsById.TryGetValue(line.SaleItemId, out var saleItem))
            {
                throw new NotFoundException(nameof(SaleItem), line.SaleItemId);
            }

            var refundable = saleItem.Quantity - saleItem.RefundedQuantity;
            if (line.Quantity > refundable)
            {
                throw new ConflictException(
                    $"Only {refundable} unit(s) of '{saleItem.ProductNameSnapshot}' remain refundable on this sale.");
            }
        }

        var refund = new Refund
        {
            BusinessId = businessId,
            BranchId = sale.BranchId,
            SaleId = sale.Id,
            RefundNumber = await GenerateRefundNumberAsync(businessId, cancellationToken),
            Reason = request.Reason,
            ProcessedByUserId = userId,
        };

        decimal totalAmount = 0;

        foreach (var line in request.Items)
        {
            var saleItem = itemsById[line.SaleItemId];
            var amount = line.Quantity * saleItem.UnitPrice;
            totalAmount += amount;

            refund.Items.Add(new RefundItem { Refund = refund, SaleItemId = saleItem.Id, Quantity = line.Quantity, Amount = amount });
            saleItem.RefundedQuantity += line.Quantity;

            var stock = await db.ProductStocks.FirstOrDefaultAsync(
                s => s.ProductId == saleItem.ProductId && s.BranchId == sale.BranchId, cancellationToken);

            if (stock is not null)
            {
                var newQuantity = stock.QuantityOnHand + line.Quantity;
                stock.QuantityOnHand = newQuantity;

                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    BusinessId = businessId,
                    ProductId = saleItem.ProductId,
                    BranchId = sale.BranchId,
                    Type = InventoryTransactionType.Refund,
                    QuantityChange = line.Quantity,
                    QuantityAfter = newQuantity,
                    Reason = $"Refund {refund.RefundNumber} against sale {sale.SaleNumber}",
                    ReferenceType = "Refund",
                    ReferenceId = refund.Id,
                    CreatedByUserId = userId,
                });
            }
        }

        refund.TotalAmount = totalAmount;
        sale.Status = sale.Items.All(i => i.RefundedQuantity >= i.Quantity) ? SaleStatus.Refunded : SaleStatus.PartiallyRefunded;

        db.Refunds.Add(refund);
        await db.SaveChangesAsync(cancellationToken);

        return new RefundDto(refund.Id, refund.RefundNumber, sale.Id, sale.SaleNumber, refund.Reason, refund.TotalAmount, refund.CreatedAt);
    }

    private async Task<string> GenerateRefundNumberAsync(Guid businessId, CancellationToken ct)
    {
        var count = await db.Refunds.IgnoreQueryFilters().CountAsync(r => r.BusinessId == businessId, ct);
        return $"R-{count + 1:D6}";
    }
}
