namespace ShopKeeper.Domain.Common;

/// <summary>
/// Marks an entity as belonging to a single Business (tenant). Every query against
/// entities implementing this interface must be filtered by BusinessId - see
/// AppDbContext's global query filters. Never bypass this filter with IgnoreQueryFilters
/// outside of trusted, explicitly-tenant-scoped administrative code paths.
/// </summary>
public interface ITenantEntity
{
    Guid BusinessId { get; set; }
}
