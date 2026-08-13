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

public record SaleLineInput(Guid ProductId, int Quantity, decimal DiscountAmount);

public record SalePaymentInput(PaymentMethod Method, decimal Amount, string? ReferenceNumber);

public record CreateSaleCommand(
    Guid BranchId,
    IReadOnlyList<SaleLineInput> Items,
    decimal DiscountAmount,
    IReadOnlyList<SalePaymentInput> Payments,
    Guid? CustomerId = null) : IRequest<SaleDto>;

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

public class CreateSaleCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<CreateSaleCommand, SaleDto>
{
    private const decimal RoundingTolerance = 0.01m;

    public async Task<SaleDto> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.SalesCreate);
        currentUser.RequireBranchAccess(request.BranchId);
        var businessId = currentUser.RequireBusinessId();
        var userId = currentUser.RequireUserId();

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
            SaleNumber = await GenerateSaleNumberAsync(businessId, cancellationToken),
        };

        decimal subtotal = 0, totalCost = 0, lineDiscountTotal = 0;

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
                var newQuantity = stock.QuantityOnHand - line.Quantity;
                stock.QuantityOnHand = newQuantity;

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
        await db.SaveChangesAsync(cancellationToken);

        var cashier = await db.Users.Where(u => u.Id == userId).Select(u => new { u.FirstName, u.LastName }).FirstAsync(cancellationToken);
        var customerName = request.CustomerId.HasValue
            ? await db.Customers.Where(c => c.Id == request.CustomerId).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken)
            : null;

        return SaleMapper.ToDto(sale, branch.Name, $"{cashier.FirstName} {cashier.LastName}", customerName);
    }

    private async Task<string> GenerateSaleNumberAsync(Guid businessId, CancellationToken ct)
    {
        var count = await db.Sales.IgnoreQueryFilters().CountAsync(s => s.BusinessId == businessId, ct);
        return $"S-{count + 1:D6}";
    }
}
