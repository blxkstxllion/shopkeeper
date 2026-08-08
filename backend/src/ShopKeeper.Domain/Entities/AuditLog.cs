namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

/// <summary>
/// Immutable record of a sensitive action. AuditLogs are append-only - nothing should
/// ever update or delete a row here. BusinessId is nullable to allow logging
/// platform-level events (e.g. a login before any business is selected).
/// </summary>
public class AuditLog : BaseEntity
{
    public Guid? BusinessId { get; set; }
    public Guid? UserId { get; set; }

    public string Action { get; set; } = default!;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }

    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
