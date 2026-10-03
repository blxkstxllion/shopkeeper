namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

public class Customer : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public string Name { get; set; } = default!;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Materialized running total from CustomerLedgerEntry, not the source of truth
    /// itself - always reconstructable by summing the ledger. Positive means the customer owes
    /// the business money. Maintained transactionally alongside every CustomerLedgerEntry
    /// insert, the same relationship ProductStock.QuantityOnHand has to InventoryTransaction.</summary>
    public decimal CurrentBalance { get; set; }

    /// <summary>Optimistic concurrency token for CurrentBalance - same mechanism and reasoning
    /// as ProductStock.RowVersion. Protects against a credit sale, a repayment, and a refund
    /// all racing to update the same customer's balance concurrently.</summary>
    public int RowVersion { get; set; }

    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    public ICollection<CustomerLedgerEntry> LedgerEntries { get; set; } = new List<CustomerLedgerEntry>();
}
