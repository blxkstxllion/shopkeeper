namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;
using ShopKeeper.Domain.Enums;

/// <summary>
/// Membership of a User in a Business with a specific Role. This is the join point
/// that grants tenant access - authorization checks always resolve through this table.
/// </summary>
public class BusinessUser : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = default!;

    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public bool IsOwner { get; set; }
    public BusinessUserStatus Status { get; set; } = BusinessUserStatus.Invited;

    public DateTimeOffset InvitedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? JoinedAt { get; set; }
}
