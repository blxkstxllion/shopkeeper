namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

/// <summary>
/// One row per recipient - a business-wide event (e.g. a new join request) that should alert
/// every owner fans out into one Notification per owner, rather than one shared row with a
/// separate read-state table. Simpler at this scale, and read/unread is then just a column.
/// </summary>
public class Notification : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public string Type { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;

    /// <summary>Frontend route to send the user to when they click the notification, e.g.
    /// "/app/employees". Null when there's nowhere more specific to go.</summary>
    public string? Link { get; set; }

    public DateTimeOffset? ReadAt { get; set; }
}
