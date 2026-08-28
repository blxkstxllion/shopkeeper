namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

/// <summary>
/// A return against a completed Sale. Reverses revenue (via TotalAmount, surfaced in reports
/// as negative revenue) and inventory (each RefundItem restocks its SaleItem's product) without
/// ever deleting or mutating the original Sale/SaleItem rows - see section 40 of the product
/// spec: reversal mechanisms, not deletion.
/// </summary>
public class Refund : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = default!;

    public Guid SaleId { get; set; }
    public Sale Sale { get; set; } = default!;

    public string RefundNumber { get; set; } = default!;
    public string Reason { get; set; } = default!;
    public decimal TotalAmount { get; set; }

    public Guid ProcessedByUserId { get; set; }

    public ICollection<RefundItem> Items { get; set; } = new List<RefundItem>();
}
