namespace ShopKeeper.Application.Sales.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

/// <summary>Fully reverses a sale's stock and revenue effect. Only valid while the sale is
/// still in its original Completed state - a partially/fully refunded sale must be corrected
/// via further refunds, not voided.</summary>
public record VoidSaleCommand(Guid SaleId, string Reason, Guid? ClientRequestId = null) : IRequest, ISupportsClientRequestId;

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
            // Without this, a concurrent write on the same ProductStock row that read its
            // RowVersion before this void committed wouldn't be detected as a conflict - see
            // RefundSaleCommand's identical increment and its doc comment on why this matters.
            stock.RowVersion++;

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

        // A sale sold on credit (CreateSaleCommand's AllowCredit) created exactly one Charge
        // ledger entry referencing this sale. Voiding the sale must reverse that charge in full
        // - otherwise the customer is left permanently owing money for a transaction that no
        // longer exists. This is a full, deterministic reversal (not a cashier-chosen split like
        // RefundSaleCommand's ApplyToBalance), so no separate confirmation/staleness UI step is
        // needed - only the RowVersion/concurrency protection below.
        if (sale.CustomerId.HasValue)
        {
            var charge = await db.CustomerLedgerEntries.FirstOrDefaultAsync(
                e => e.ReferenceType == "Sale" && e.ReferenceId == sale.Id && e.Type == CustomerLedgerEntryType.Charge,
                cancellationToken);

            if (charge is not null)
            {
                var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == sale.CustomerId.Value, cancellationToken)
                    ?? throw new NotFoundException(nameof(Customer), sale.CustomerId.Value);

                customer.CurrentBalance -= charge.Amount;
                customer.RowVersion++;

                db.CustomerLedgerEntries.Add(new CustomerLedgerEntry
                {
                    BusinessId = businessId,
                    CustomerId = customer.Id,
                    Type = CustomerLedgerEntryType.ChargeReversal,
                    Amount = -charge.Amount,
                    BalanceAfter = customer.CurrentBalance,
                    ReferenceType = "Sale",
                    ReferenceId = sale.Id,
                    CreatedByUserId = userId,
                });
            }
        }

        sale.Status = SaleStatus.Voided;
        sale.VoidedAt = DateTimeOffset.UtcNow;
        sale.VoidedByUserId = userId;
        sale.VoidReason = request.Reason;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent sale/refund moved one of these ProductStock rows, or a concurrent
            // charge/payment/refund moved the customer's balance, between our read and this
            // write - see RefundSaleCommand's identical handling.
            throw new ConflictException("Stock or account balance changed while this sale was being voided. Please try again.");
        }
    }
}
