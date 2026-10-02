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

    /// <summary>Same mechanism as Sale.ClientRequestId - a dedicated precheck + partial unique
    /// index backstop, not the generic IdempotencyBehavior. A refund has cascading side effects
    /// (stock increment, Sale.Status change) that compound badly if the same submission is
    /// processed twice, and the generic behavior's response-persisted-after-commit gap is only
    /// actually safe for a single device's sequential sync loop - two tabs of the same account
    /// coming online simultaneously would violate that assumption. See IdempotencyBehavior's
    /// own doc comment for the tradeoff this specifically avoids.</summary>
    public Guid? ClientRequestId { get; set; }

    public ICollection<RefundItem> Items { get; set; } = new List<RefundItem>();
}
