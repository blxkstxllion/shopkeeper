namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

/// <summary>
/// A named set of permissions within a single Business. Default roles (Owner,
/// Administrator, Branch Manager, Cashier, Inventory Manager, Accountant, Viewer)
/// are seeded per-business at onboarding so each tenant can customize them
/// independently without affecting other tenants.
/// </summary>
public class Role : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<BusinessUser> BusinessUsers { get; set; } = new List<BusinessUser>();
}
