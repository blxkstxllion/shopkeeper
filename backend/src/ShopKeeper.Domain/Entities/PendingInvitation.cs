namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

/// <summary>
/// An outstanding invite for someone to join a Business. Becomes a BusinessUser once
/// accepted (AcceptedAt is set, but the row itself is kept for history rather than deleted).
/// </summary>
public class PendingInvitation : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public string Email { get; set; } = default!;

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = default!;

    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public string Token { get; set; } = default!;
    public DateTimeOffset ExpiresAt { get; set; }

    public Guid InvitedByUserId { get; set; }
    public User InvitedByUser { get; set; } = default!;

    public DateTimeOffset? AcceptedAt { get; set; }
}
