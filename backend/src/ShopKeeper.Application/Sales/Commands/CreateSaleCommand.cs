namespace ShopKeeper.Application.Sales.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Sales.Dtos;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

public record SaleLineInput(Guid ProductId, int Quantity, decimal DiscountAmount);

public record SalePaymentInput(PaymentMethod Method, decimal Amount, string? ReferenceNumber);

public record CreateSaleCommand(
    Guid BranchId,
    IReadOnlyList<SaleLineInput> Items,
    decimal DiscountAmount,
    IReadOnlyList<SalePaymentInput> Payments,
    Guid? CustomerId = null,
    Guid? ClientRequestId = null) : IRequest<SaleDto>;

public class CreateSaleCommandValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("A sale needs at least one item.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.DiscountAmount).GreaterThanOrEqualTo(0);
        });
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Payments).NotEmpty().WithMessage("At least one payment is required.");
        RuleForEach(x => x.Payments).ChildRules(payment => payment.RuleFor(p => p.Amount).GreaterThan(0));
    }
}

public class CreateSaleCommandHandler(IAppDbContext db, ICurrentUserService currentUser, NotificationDispatcher notifications)
    : IRequestHandler<CreateSaleCommand, SaleDto>
{
    private const decimal RoundingTolerance = 0.01m;

    public async Task<SaleDto> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.SalesCreate);
        currentUser.RequireBranchAccess(request.BranchId);
        var businessId = currentUser.RequireBusinessId();
        var userId = currentUser.RequireUserId();

        // Idempotent replay: an offline-queued sale carries a client-generated key so retrying
        // a sync whose response was lost (not whose request failed) returns the sale that
        // already exists instead of erroring or double-selling. Checked before any other work -
        // in particular before ClaimNextSaleNumberAsync, which burns a number on every call.
        if (request.ClientRequestId.HasValue)
        {
            var existing = await FindByClientRequestIdAsync(businessId, request.ClientRequestId.Value, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }
        }

        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken)
            ?? throw new NotFoundException(nameof(Branch), request.BranchId);

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var line in request.Items)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                throw new NotFoundException(nameof(Product), line.ProductId);
            }

            if (!product.IsActive)
            {
                throw new ConflictException($"'{product.Name}' is not available for sale.");
            }
        }

        var stockByProduct = new Dictionary<Guid, ProductStock>();
        foreach (var line in request.Items.Where(l => products[l.ProductId].TrackInventory))
        {
            var stock = await db.ProductStocks.FirstOrDefaultAsync(
                s => s.ProductId == line.ProductId && s.BranchId == request.BranchId, cancellationToken);

            var available = stock?.QuantityOnHand ?? 0;
            if (available < line.Quantity)
            {
                throw new ConflictException(
                    $"Not enough stock for '{products[line.ProductId].Name}': {available} available, {line.Quantity} requested.");
            }

            stockByProduct[line.ProductId] = stock!;
        }

        var businessSetting = await db.BusinessSettings.FirstOrDefaultAsync(s => s.BusinessId == businessId, cancellationToken);

        var sale = new Sale
        {
            BusinessId = businessId,
            BranchId = request.BranchId,
            Branch = branch,
            CustomerId = request.CustomerId,
            CashierUserId = userId,
            SaleNumber = await ClaimNextSaleNumberAsync(businessId, cancellationToken),
            ClientRequestId = request.ClientRequestId,
        };

        decimal subtotal = 0, totalCost = 0, lineDiscountTotal = 0;
        var lowStockAlerts = new List<(string ProductName, int NewQuantity, int ReorderLevel)>();

        foreach (var line in request.Items)
        {
            var product = products[line.ProductId];
            var lineRevenue = (product.SellingPrice * line.Quantity) - line.DiscountAmount;
            var lineCost = product.CostPrice * line.Quantity;

            subtotal += product.SellingPrice * line.Quantity;
            totalCost += lineCost;
            lineDiscountTotal += line.DiscountAmount;

            sale.Items.Add(new SaleItem
            {
                Sale = sale,
                ProductId = product.Id,
                ProductNameSnapshot = product.Name,
                SkuSnapshot = product.Sku,
                Quantity = line.Quantity,
                UnitPrice = product.SellingPrice,
                UnitCost = product.CostPrice,
                DiscountAmount = line.DiscountAmount,
                LineRevenue = lineRevenue,
                LineCost = lineCost,
                LineProfit = lineRevenue - lineCost,
            });

            if (stockByProduct.TryGetValue(line.ProductId, out var stock))
            {
                var previousQuantity = stock.QuantityOnHand;
                var newQuantity = previousQuantity - line.Quantity;
                stock.QuantityOnHand = newQuantity;
                stock.RowVersion++;

                // Fires once, on the sale that actually crosses the reorder level going down -
                // not on every subsequent sale while stock is already low.
                if (product.ReorderLevel > 0 && newQuantity <= product.ReorderLevel && previousQuantity > product.ReorderLevel)
                {
                    lowStockAlerts.Add((product.Name, newQuantity, product.ReorderLevel));
                }

                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    BusinessId = businessId,
                    ProductId = product.Id,
                    BranchId = request.BranchId,
                    Type = InventoryTransactionType.Sale,
                    QuantityChange = -line.Quantity,
                    QuantityAfter = newQuantity,
                    Reason = $"Sale {sale.SaleNumber}",
                    ReferenceType = "Sale",
                    ReferenceId = sale.Id,
                    CreatedByUserId = userId,
                });
            }
        }

        var totalDiscount = lineDiscountTotal + request.DiscountAmount;
        var taxableAmount = subtotal - totalDiscount;

        decimal taxAmount = 0, total;
        if (businessSetting is { TaxEnabled: true } && businessSetting.TaxRatePercent > 0)
        {
            var rate = businessSetting.TaxRatePercent / 100m;
            if (businessSetting.TaxInclusivePricing)
            {
                // Prices already include tax - back the tax portion out rather than adding it on top.
                taxAmount = Math.Round(taxableAmount * rate / (1 + rate), 2);
                total = taxableAmount;
            }
            else
            {
                taxAmount = Math.Round(taxableAmount * rate, 2);
                total = taxableAmount + taxAmount;
            }
        }
        else
        {
            total = taxableAmount;
        }

        var paymentsTotal = request.Payments.Sum(p => p.Amount);
        if (Math.Abs(paymentsTotal - total) > RoundingTolerance)
        {
            throw new ConflictException($"Payments total {paymentsTotal:0.00} but the sale total is {total:0.00}.");
        }

        sale.Subtotal = subtotal;
        sale.DiscountAmount = totalDiscount;
        sale.TaxAmount = taxAmount;
        sale.Total = total;
        sale.TotalCost = totalCost;
        sale.GrossProfit = (subtotal - totalDiscount) - totalCost;

        foreach (var payment in request.Payments)
        {
            sale.Payments.Add(new Payment
            {
                BusinessId = businessId,
                Sale = sale,
                Method = payment.Method,
                Amount = payment.Amount,
                ReferenceNumber = payment.ReferenceNumber,
            });
        }

        db.Sales.Add(sale);

        foreach (var alert in lowStockAlerts)
        {
            await notifications.NotifyOwnersAsync(
                businessId, "LowStock", "Low stock alert",
                $"'{alert.ProductName}' is down to {alert.NewQuantity} units (reorder level: {alert.ReorderLevel}).",
                "/app/inventory", cancellationToken);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent sale or stock adjustment moved one of this sale's ProductStock
            // rows between our read and this write - ProductStock.RowVersion is what
            // catches it. Surface a clean conflict rather than the raw EF exception; the
            // caller (POS UI) can just retry with fresh stock numbers.
            throw new ConflictException("Stock changed while this sale was being processed. Please try again.");
        }
        catch (DbUpdateException) when (request.ClientRequestId.HasValue)
        {
            // Two concurrent syncs replaying the same offline sale both passed the precheck
            // above and raced to insert - the partial unique index on (BusinessId,
            // ClientRequestId) caught it. This is not a real conflict from the client's point
            // of view: the sale IS created, just by the other request. Return the winner's
            // data instead of an error. If no such sale exists, this was a genuinely different
            // DbUpdateException and must not be swallowed.
            var winner = await FindByClientRequestIdAsync(businessId, request.ClientRequestId.Value, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return winner;
        }

        var cashier = await db.Users.Where(u => u.Id == userId).Select(u => new { u.FirstName, u.LastName }).FirstAsync(cancellationToken);
        var customerName = request.CustomerId.HasValue
            ? await db.Customers.Where(c => c.Id == request.CustomerId).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken)
            : null;

        return SaleMapper.ToDto(sale, branch.Name, $"{cashier.FirstName} {cashier.LastName}", customerName);
    }

    private async Task<SaleDto?> FindByClientRequestIdAsync(Guid businessId, Guid clientRequestId, CancellationToken ct)
    {
        var existing = await db.Sales.Include(s => s.Items).Include(s => s.Payments).Include(s => s.Branch)
            .FirstOrDefaultAsync(s => s.BusinessId == businessId && s.ClientRequestId == clientRequestId, ct);
        if (existing is null)
        {
            return null;
        }

        var cashier = await db.Users.Where(u => u.Id == existing.CashierUserId)
            .Select(u => new { u.FirstName, u.LastName }).FirstAsync(ct);
        var customerName = existing.CustomerId.HasValue
            ? await db.Customers.Where(c => c.Id == existing.CustomerId).Select(c => c.Name).FirstOrDefaultAsync(ct)
            : null;

        return SaleMapper.ToDto(existing, existing.Branch.Name, $"{cashier.FirstName} {cashier.LastName}", customerName);
    }

    /// <summary>
    /// Claims the next sale number via BusinessSetting.NextSaleNumber (optimistic-concurrency
    /// protected, see BusinessSetting.RowVersion) instead of COUNT(*)+1, which two concurrent
    /// sales could both read as the same value and both then collide on the
    /// (BusinessId, SaleNumber) unique index. Retries on DbUpdateConcurrencyException - the
    /// same mechanism protecting ProductStock, reused here rather than a second technique.
    /// Deliberately committed as its own SaveChangesAsync, not folded into the rest of the
    /// sale's save: if the sale fails afterward for an unrelated reason, this number is burned
    /// (a gap in the sequence), which is normal/accepted for invoice-style numbering.
    /// </summary>
    private async Task<string> ClaimNextSaleNumberAsync(Guid businessId, CancellationToken ct)
    {
        var setting = await db.BusinessSettings.FirstOrDefaultAsync(s => s.BusinessId == businessId, ct)
            ?? throw new ConflictException("This business has no settings configured.");

        const int maxAttempts = 5;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var claimed = setting.NextSaleNumber;
            setting.NextSaleNumber = claimed + 1;
            setting.RowVersion++;

            try
            {
                await db.SaveChangesAsync(ct);
                return $"S-{claimed:D6}";
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Someone else claimed a number first - reload this tracked entity from its
                // current DB values so the next attempt retries against fresh data instead
                // of the same stale in-memory instance.
                await ex.Entries.Single().ReloadAsync(ct);
            }
        }

        throw new ConflictException("Unable to generate a sale number right now. Please try again.");
    }
}
