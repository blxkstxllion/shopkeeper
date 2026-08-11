namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

/// <summary>
/// A line on a Sale. Product name/SKU/price/cost are snapshotted at sale time so that later
/// edits to the Product (rename, reprice) never rewrite historical revenue, COGS, or profit.
/// </summary>
public class SaleItem : BaseEntity
{
    public Guid SaleId { get; set; }
    public Sale Sale { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public string ProductNameSnapshot { get; set; } = default!;
    public string SkuSnapshot { get; set; } = default!;

    public int Quantity { get; set; }

    /// <summary>Selling price per unit at the moment of sale.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Cost price per unit at the moment of sale - locks in COGS regardless of later cost changes.</summary>
    public decimal UnitCost { get; set; }

    public decimal DiscountAmount { get; set; }

    /// <summary>(UnitPrice * Quantity) - DiscountAmount.</summary>
    public decimal LineRevenue { get; set; }

    /// <summary>UnitCost * Quantity.</summary>
    public decimal LineCost { get; set; }

    /// <summary>LineRevenue - LineCost.</summary>
    public decimal LineProfit { get; set; }

    /// <summary>How much of Quantity has been returned via RefundItems - never exceeds Quantity.</summary>
    public int RefundedQuantity { get; set; }
}
