namespace ShopKeeper.Application.Sales.Dtos;

public record SaleItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string Sku,
    int Quantity,
    decimal UnitPrice,
    decimal UnitCost,
    decimal DiscountAmount,
    decimal LineRevenue,
    decimal LineCost,
    decimal LineProfit,
    int RefundedQuantity,
    // The line's actual share of Sale.Total (net of every discount, inclusive of tax) - what
    // RefundSaleCommand refunds from. Exposed so the frontend can show an accurate refund-amount
    // preview before submitting, rather than approximating from LineRevenue (which doesn't
    // reflect sale-level discount or tax).
    decimal NetLineValue);

public record PaymentDto(Guid Id, string Method, decimal Amount, string? ReferenceNumber);

public record SaleDto(
    Guid Id,
    string SaleNumber,
    Guid BranchId,
    string BranchName,
    Guid? CustomerId,
    string? CustomerName,
    Guid CashierUserId,
    string CashierName,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal Total,
    decimal TotalCost,
    decimal GrossProfit,
    string Status,
    DateTimeOffset? VoidedAt,
    string? VoidReason,
    DateTimeOffset CreatedAt,
    IReadOnlyList<SaleItemDto> Items,
    IReadOnlyList<PaymentDto> Payments);

public record SaleListItemDto(
    Guid Id,
    string SaleNumber,
    string BranchName,
    string CashierName,
    decimal Total,
    decimal GrossProfit,
    string Status,
    int ItemCount,
    DateTimeOffset CreatedAt);

public record SellableProductDto(
    Guid ProductId,
    string Name,
    string Sku,
    string? Barcode,
    string? ImageUrl,
    Guid? CategoryId,
    decimal SellingPrice,
    bool TrackInventory,
    int? QuantityOnHand);

public record RefundDto(
    Guid Id,
    string RefundNumber,
    Guid SaleId,
    string SaleNumber,
    string Reason,
    decimal TotalAmount,
    decimal AmountAppliedToBalance,
    DateTimeOffset CreatedAt);
