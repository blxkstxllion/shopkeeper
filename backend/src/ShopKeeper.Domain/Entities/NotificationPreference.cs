namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

/// <summary>
/// One row per (Business, User) - which of the fixed set of notification types this user wants
/// to receive for this business. Absence of a row means every type defaults to enabled (see
/// NotificationDispatcher), so a row only needs to exist once someone actually mutes something.
/// A flat bool per type, not a generic "NotificationType + enabled" table, because there are only
/// two notification types today (JoinRequestSubmitted, LowStock) - the same reasoning PlanLimits
/// uses for staying a static lookup instead of a DB table until real variability exists.
/// </summary>
public class NotificationPreference : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public bool NotifyOnJoinRequest { get; set; } = true;
    public bool NotifyOnLowStock { get; set; } = true;
}
