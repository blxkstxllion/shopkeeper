namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

/// <summary>
/// Materialized on-hand quantity per product per branch, maintained transactionally
/// alongside every InventoryTransaction. InventoryTransaction is the source of truth /
/// audit ledger; this exists purely so reads (POS product grid, inventory list) don't
/// have to sum the whole transaction history on every request.
/// </summary>
public class ProductStock : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = default!;

    public int QuantityOnHand { get; set; }

    /// <summary>Optimistic concurrency token, incremented by hand on every QuantityOnHand
    /// change. EF Core includes it in the generated UPDATE's WHERE clause and throws
    /// DbUpdateConcurrencyException if the row moved since it was read - this is what stops
    /// two simultaneous sales from both reading the same stock and both succeeding. A plain
    /// int rather than Postgres's native `xmin` because the same model has to work against
    /// the SQLite provider the test suite runs on.</summary>
    public int RowVersion { get; set; }
}
