namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

/// <summary>
/// A single grantable capability (e.g. "sales:create"). Permissions are a global,
/// platform-defined catalog - not tenant-scoped - so every business chooses from the
/// same list when composing roles.
/// </summary>
public class Permission : BaseEntity
{
    public string Key { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
