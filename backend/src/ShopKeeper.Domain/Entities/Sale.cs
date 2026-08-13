namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;
using ShopKeeper.Domain.Enums;

public class Sale : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = default!;

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string SaleNumber { get; set; } = default!;

    public Guid CashierUserId { get; set; }
    public User CashierUser { get; set; } = default!;

    /// <summary>Sum of (UnitPrice * Quantity) across items, before discount and tax.</summary>
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }

    /// <summary>Subtotal - DiscountAmount + TaxAmount. What the customer actually paid.</summary>
    public decimal Total { get; set; }

    /// <summary>Sum of (UnitCost * Quantity) across items - the sale's cost of goods sold.</summary>
    public decimal TotalCost { get; set; }

    /// <summary>(Subtotal - DiscountAmount) - TotalCost. Tax is a pass-through, not profit.</summary>
    public decimal GrossProfit { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Completed;

    public DateTimeOffset? VoidedAt { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Refund> Refunds { get; set; } = new List<Refund>();
}
