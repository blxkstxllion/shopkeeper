namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

public class RefundItem : BaseEntity
{
    public Guid RefundId { get; set; }
    public Refund Refund { get; set; } = default!;

    public Guid SaleItemId { get; set; }
    public SaleItem SaleItem { get; set; } = default!;

    public int Quantity { get; set; }

    /// <summary>Quantity * SaleItem.UnitPrice - the refunded amount for this line.</summary>
    public decimal Amount { get; set; }
}
