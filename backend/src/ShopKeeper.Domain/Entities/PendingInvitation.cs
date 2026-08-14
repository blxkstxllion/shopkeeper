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

    /// <summary>SHA-256 hash of the raw invite token (same IJwtTokenService.Hash used for
    /// RefreshToken.TokenHash) - the raw value is only ever held in memory long enough to email
    /// it, never persisted, so a database read can't leak a usable invite link.</summary>
    public string TokenHash { get; set; } = default!;
    public DateTimeOffset ExpiresAt { get; set; }

    public Guid InvitedByUserId { get; set; }
    public User InvitedByUser { get; set; } = default!;

    public DateTimeOffset? AcceptedAt { get; set; }
}
