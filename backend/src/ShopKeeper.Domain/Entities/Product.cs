namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

/// <summary>
/// The shared catalog entry for a business (not per-branch). Selling/cost price live here as
/// the *current* values; every SaleItem snapshots the prices at the moment of sale so that
/// changing a product's price later never rewrites historical revenue or profit figures.
/// </summary>
public class Product : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public Guid? CategoryId { get; set; }
    public ProductCategory? Category { get; set; }

    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public string Name { get; set; } = default!;
    public string Sku { get; set; } = default!;
    public string? Barcode { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }

    public decimal SellingPrice { get; set; }
    public decimal CostPrice { get; set; }

    /// <summary>Stock falls to this number or below -> the dashboard/inventory page flags it
    /// as low stock and a one-time notification fires (see AdjustStockCommand/CreateSaleCommand).
    /// Was two separate, confusing fields ("Minimum stock" and "Reorder level") until a real
    /// user found the unused one impossible to tell apart from this one - consolidated.</summary>
    public int MinimumStock { get; set; }

    /// <summary>False for services/non-stock items - inventory transactions and stock checks are skipped.</summary>
    public bool TrackInventory { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public ICollection<ProductStock> StockByBranch { get; set; } = new List<ProductStock>();
}
