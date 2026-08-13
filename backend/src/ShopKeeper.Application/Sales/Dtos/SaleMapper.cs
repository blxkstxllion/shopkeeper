namespace ShopKeeper.Application.Sales.Dtos;

using ShopKeeper.Domain.Entities;

public static class SaleMapper
{
    public static SaleDto ToDto(Sale sale, string branchName, string cashierName, string? customerName = null) => new(
        sale.Id, sale.SaleNumber, sale.BranchId, branchName, sale.CustomerId, customerName, sale.CashierUserId, cashierName,
        sale.Subtotal, sale.DiscountAmount, sale.TaxAmount, sale.Total, sale.TotalCost, sale.GrossProfit,
        sale.Status.ToString(), sale.VoidedAt, sale.VoidReason, sale.CreatedAt,
        sale.Items.Select(ToItemDto).ToList(),
        sale.Payments.Select(p => new PaymentDto(p.Id, p.Method.ToString(), p.Amount, p.ReferenceNumber)).ToList());

    public static SaleItemDto ToItemDto(SaleItem item) => new(
        item.Id, item.ProductId, item.ProductNameSnapshot, item.SkuSnapshot, item.Quantity,
        item.UnitPrice, item.UnitCost, item.DiscountAmount, item.LineRevenue, item.LineCost, item.LineProfit,
        item.RefundedQuantity);
}
