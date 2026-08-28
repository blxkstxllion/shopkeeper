namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;
using ShopKeeper.Domain.Enums;

/// <summary>
/// A worker's self-service request to join a Business, submitted via a shared join code
/// instead of a targeted owner-initiated PendingInvitation. Sits Pending until the owner
/// approves it (creating the real BusinessUser, since RoleId isn't known until then) or
/// rejects it. The User row already exists by the time this is created - see
/// SubmitJoinRequestCommand.
/// </summary>
public class JoinRequest : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public JoinRequestStatus Status { get; set; } = JoinRequestStatus.Pending;

    public Guid? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}
