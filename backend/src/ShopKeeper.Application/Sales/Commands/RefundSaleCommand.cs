namespace ShopKeeper.Application.Sales.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Sales.Dtos;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

public record RefundLineInput(Guid SaleItemId, int Quantity);

public record RefundSaleCommand(Guid SaleId, IReadOnlyList<RefundLineInput> Items, string Reason, Guid? ClientRequestId = null)
    : IRequest<RefundDto>, ISupportsClientRequestId;

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

        currentUser.RequireBranchAccess(sale.BranchId);

        if (sale.Status is not (SaleStatus.Completed or SaleStatus.PartiallyRefunded))
        {
            throw new ConflictException($"Sale {sale.SaleNumber} cannot be refunded from its current status ({sale.Status}).");
        }

        var itemsById = sale.Items.ToDictionary(i => i.Id);

        // Aggregated by SaleItemId first, not checked per line - two lines for the same sale
        // item (e.g. a duplicated row in the refund form) must be validated against their
        // combined quantity. Checking each line independently against the same starting
        // RefundedQuantity let both pass even when their total exceeded what was actually sold,
        // the same class of bug CreateSaleCommand had for duplicate sale lines.
        var requestedQuantityBySaleItem = request.Items
            .GroupBy(l => l.SaleItemId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        foreach (var (saleItemId, requestedQuantity) in requestedQuantityBySaleItem)
        {
            if (!itemsById.TryGetValue(saleItemId, out var saleItem))
            {
                throw new NotFoundException(nameof(SaleItem), saleItemId);
            }

            var refundable = saleItem.Quantity - saleItem.RefundedQuantity;
            if (requestedQuantity > refundable)
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
            // Derived from NetAmountPaid (the line's actual share of Sale.Total, already net of
            // every discount and inclusive of tax - see SaleItem's doc comment), not UnitPrice.
            // UnitPrice is the gross pre-discount, pre-tax price - refunding from it would hand
            // back more than the customer actually paid whenever a discount or tax applied.
            var amount = Math.Round((saleItem.NetAmountPaid / saleItem.Quantity) * line.Quantity, 2);
            totalAmount += amount;

            refund.Items.Add(new RefundItem { Refund = refund, SaleItemId = saleItem.Id, Quantity = line.Quantity, Amount = amount });
            saleItem.RefundedQuantity += line.Quantity;

            var stock = await db.ProductStocks.FirstOrDefaultAsync(
                s => s.ProductId == saleItem.ProductId && s.BranchId == sale.BranchId, cancellationToken);

            if (stock is not null)
            {
                var newQuantity = stock.QuantityOnHand + line.Quantity;
                stock.QuantityOnHand = newQuantity;
                // Without this, a concurrent write on the same ProductStock row that read its
                // RowVersion before this refund committed wouldn't be detected as a conflict -
                // EF's optimistic-concurrency check only catches writes against a RowVersion
                // that's actually changed. See CreateSaleCommand's identical increment.
                stock.RowVersion++;

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

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent sale or another refund moved one of these ProductStock rows between
            // our read and this write - see CreateSaleCommand's identical handling. Only
            // actually reachable now that the RowVersion increment above exists; before it, a
            // concurrent write here was silently never detected at all (a lost-update bug, not
            // an absence of conflicts).
            throw new ConflictException("Stock changed while this refund was being processed. Please try again.");
        }

        return new RefundDto(refund.Id, refund.RefundNumber, sale.Id, sale.SaleNumber, refund.Reason, refund.TotalAmount, refund.CreatedAt);
    }

    private async Task<string> GenerateRefundNumberAsync(Guid businessId, CancellationToken ct)
    {
        var count = await db.Refunds.IgnoreQueryFilters().CountAsync(r => r.BusinessId == businessId, ct);
        return $"R-{count + 1:D6}";
    }
}
