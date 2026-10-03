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

// Deliberately NOT ISupportsClientRequestId - this has its own dedicated idempotency mechanism
// below (precheck by (BusinessId, ClientRequestId), a partial unique index backstop, catch the
// race), mirroring CreateSaleCommand rather than the generic IdempotencyBehavior. A refund has
// cascading side effects (stock increment, Sale.Status change) that compound badly if processed
// twice, and the generic behavior's response-persisted-after-commit gap is only safe for a
// single device's sequential sync loop - see Refund.ClientRequestId's doc comment.
public record RefundSaleCommand(
    Guid SaleId,
    IReadOnlyList<RefundLineInput> Items,
    string Reason,
    // Explicit, never inferred or defaulted - the cashier sees the computed refund total, the
    // customer's current balance, and actively chooses how much of the refund applies to the
    // account versus pays out in cash/card. CustomerBalanceRowVersion is the balance's
    // RowVersion as displayed on that confirmation screen; if it's moved since, the request is
    // rejected so the UI can re-fetch and the cashier re-confirms against current numbers
    // rather than silently applying a stale split.
    decimal ApplyToBalance = 0,
    int? CustomerBalanceRowVersion = null,
    Guid? ClientRequestId = null)
    : IRequest<RefundDto>;

public class RefundSaleCommandValidator : AbstractValidator<RefundSaleCommand>
{
    public RefundSaleCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Items).NotEmpty().WithMessage("A refund needs at least one item.");
        RuleForEach(x => x.Items).ChildRules(item => item.RuleFor(i => i.Quantity).GreaterThan(0));
        RuleFor(x => x.ApplyToBalance).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CustomerBalanceRowVersion).NotNull()
            .When(x => x.ApplyToBalance > 0)
            .WithMessage("The customer's balance version must be confirmed when applying a refund to their account.");
    }
}

public class RefundSaleCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<RefundSaleCommand, RefundDto>
{
    public async Task<RefundDto> Handle(RefundSaleCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.SalesRefund);
        var businessId = currentUser.RequireBusinessId();
        var userId = currentUser.RequireUserId();

        // Idempotent replay: see RefundSaleCommand's own doc comment for why this has its own
        // mechanism rather than the generic IdempotencyBehavior. Checked before any other work,
        // same as CreateSaleCommand's identical precheck.
        if (request.ClientRequestId.HasValue)
        {
            var existing = await FindByClientRequestIdAsync(businessId, request.ClientRequestId.Value, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }
        }

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
            ClientRequestId = request.ClientRequestId,
        };

        // Cumulative, not per-call independent rounding: computing Math.Round(unitShare *
        // thisCall'sQuantity, 2) separately on every refund call lets rounding drift accumulate
        // across repeated partial refunds of the same line (e.g. three separate 1-unit refunds
        // of a $10.00/3-unit line summed to $9.99, a cent short, with the old per-call formula -
        // a real bug that shipped). Instead, each call computes what the TOTAL refunded-so-far
        // amount for the line should be (rounded once, from the cumulative quantity), then
        // subtracts what's already been recorded - the difference is this call's amount. When
        // the cumulative quantity reaches the line's full Quantity, the cumulative amount is
        // NetAmountPaid exactly (no rounding at all), so full completion always reconciles to
        // the cent regardless of how many partial refunds got there.
        // Summed client-side, not via a server-side GroupBy/Sum - the SQLite test provider can't
        // translate Sum over a decimal column (the same limitation GetProfitabilityReportQuery
        // and ScheduledReportScheduling's own tests already have to work around), and this is a
        // handful of rows per sale item, not a table scan.
        var saleItemIds = requestedQuantityBySaleItem.Keys.ToList();
        var refundedAmountBySaleItem = (await db.RefundItems
                .Where(ri => saleItemIds.Contains(ri.SaleItemId))
                .Select(ri => new { ri.SaleItemId, ri.Amount })
                .ToListAsync(cancellationToken))
            .GroupBy(ri => ri.SaleItemId)
            .ToDictionary(g => g.Key, g => g.Sum(ri => ri.Amount));

        decimal totalAmount = 0;

        foreach (var line in request.Items)
        {
            var saleItem = itemsById[line.SaleItemId];
            var priorAmount = refundedAmountBySaleItem.GetValueOrDefault(line.SaleItemId);
            var cumulativeQuantity = saleItem.RefundedQuantity + line.Quantity;

            // Derived from NetAmountPaid (the line's actual share of Sale.Total, already net of
            // every discount and inclusive of tax - see SaleItem's doc comment), not UnitPrice.
            // UnitPrice is the gross pre-discount, pre-tax price - refunding from it would hand
            // back more than the customer actually paid whenever a discount or tax applied.
            var cumulativeAmount = cumulativeQuantity >= saleItem.Quantity
                ? saleItem.NetAmountPaid
                : Math.Round(saleItem.NetAmountPaid * cumulativeQuantity / saleItem.Quantity, 2);
            var amount = cumulativeAmount - priorAmount;
            totalAmount += amount;

            refund.Items.Add(new RefundItem { Refund = refund, SaleItemId = saleItem.Id, Quantity = line.Quantity, Amount = amount });
            saleItem.RefundedQuantity = cumulativeQuantity;
            // Keeps a second line for the same SaleItemId within this same request correct too -
            // it must see this line's contribution as already-recorded, not just what was in the
            // database before this request started.
            refundedAmountBySaleItem[line.SaleItemId] = cumulativeAmount;

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

        if (request.ApplyToBalance > 0)
        {
            if (!sale.CustomerId.HasValue)
            {
                throw new ConflictException("This sale has no customer to apply a balance credit to.");
            }

            var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == sale.CustomerId.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(Customer), sale.CustomerId.Value);

            // Staleness check against what the cashier's confirmation screen actually displayed
            // - distinct from (and in addition to) EF's own optimistic-concurrency protection
            // below, which only catches a conflict within this request's own read-to-write
            // window, not one that happened before this request was ever sent.
            if (customer.RowVersion != request.CustomerBalanceRowVersion)
            {
                throw new ConflictException(
                    "This customer's balance changed since the refund split was calculated. Please review and confirm again.");
            }

            if (request.ApplyToBalance > totalAmount)
            {
                throw new ConflictException("Cannot apply more to the account balance than the refund total.");
            }

            if (request.ApplyToBalance > customer.CurrentBalance)
            {
                throw new ConflictException("Cannot apply more to the account balance than the customer currently owes.");
            }

            customer.CurrentBalance -= request.ApplyToBalance;
            customer.RowVersion++;
            refund.AmountAppliedToBalance = request.ApplyToBalance;

            db.CustomerLedgerEntries.Add(new CustomerLedgerEntry
            {
                BusinessId = businessId,
                CustomerId = customer.Id,
                Type = CustomerLedgerEntryType.RefundCredit,
                Amount = -request.ApplyToBalance,
                BalanceAfter = customer.CurrentBalance,
                ReferenceType = "Refund",
                ReferenceId = refund.Id,
                CreatedByUserId = userId,
            });
        }

        db.Refunds.Add(refund);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent sale/refund moved one of these ProductStock rows, or a concurrent
            // charge/payment/refund moved the customer's balance, between our read and this
            // write - see CreateSaleCommand's identical handling. Only actually reachable now
            // that the RowVersion increments above exist; before them, a concurrent write here
            // was silently never detected at all (a lost-update bug, not an absence of conflicts).
            throw new ConflictException("Stock or account balance changed while this refund was being processed. Please try again.");
        }
        catch (DbUpdateException) when (request.ClientRequestId.HasValue)
        {
            // Two concurrent replays of the same ClientRequestId both passed the precheck above
            // and raced to insert - the partial unique index on (BusinessId, ClientRequestId)
            // caught it. Not a real conflict from the client's point of view: the refund IS
            // created, just by the other request. Return the winner's data instead of an error.
            // Same shape as CreateSaleCommand's identical race handling.
            var winner = await FindByClientRequestIdAsync(businessId, request.ClientRequestId.Value, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return winner;
        }

        return new RefundDto(
            refund.Id, refund.RefundNumber, sale.Id, sale.SaleNumber, refund.Reason,
            refund.TotalAmount, refund.AmountAppliedToBalance, refund.CreatedAt);
    }

    private async Task<string> GenerateRefundNumberAsync(Guid businessId, CancellationToken ct)
    {
        var count = await db.Refunds.IgnoreQueryFilters().CountAsync(r => r.BusinessId == businessId, ct);
        return $"R-{count + 1:D6}";
    }

    private async Task<RefundDto?> FindByClientRequestIdAsync(Guid businessId, Guid clientRequestId, CancellationToken ct)
    {
        var existing = await db.Refunds.Include(r => r.Sale)
            .FirstOrDefaultAsync(r => r.BusinessId == businessId && r.ClientRequestId == clientRequestId, ct);
        return existing is null
            ? null
            : new RefundDto(
                existing.Id, existing.RefundNumber, existing.SaleId, existing.Sale.SaleNumber, existing.Reason,
                existing.TotalAmount, existing.AmountAppliedToBalance, existing.CreatedAt);
    }
}
