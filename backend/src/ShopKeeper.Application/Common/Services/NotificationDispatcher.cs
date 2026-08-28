namespace ShopKeeper.Application.Common.Services;

using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

/// <summary>
/// Fans a business-wide event out into one Notification row per recipient who hasn't muted this
/// type. Callers add the rows to the tracked change set but don't save - the caller's own
/// existing SaveChangesAsync (already part of the handler's flow) persists the notification(s)
/// together with whatever triggered them, so e.g. a JoinRequest and the owner's notification
/// about it either both commit or neither does.
/// </summary>
public class NotificationDispatcher(IAppDbContext db)
{
    public async Task NotifyOwnersAsync(
        Guid businessId, string type, string title, string message, string? link, CancellationToken ct)
    {
        // IgnoreQueryFilters: some callers run before any tenant context exists yet (e.g. an
        // unauthenticated join-code submission notifying the business they're requesting to
        // join) - businessId here is already a trusted, validated value from the caller.
        var ownerUserIds = await db.BusinessUsers.IgnoreQueryFilters()
            .Where(bu => bu.BusinessId == businessId && bu.IsOwner && bu.Status == BusinessUserStatus.Active)
            .Select(bu => bu.UserId)
            .ToListAsync(ct);

        // Absence of a preference row means every type is still enabled by default - only load
        // rows for people who've actually muted something.
        var mutedByUserId = await db.NotificationPreferences.IgnoreQueryFilters()
            .Where(p => p.BusinessId == businessId && ownerUserIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, ct);

        foreach (var userId in ownerUserIds)
        {
            if (mutedByUserId.TryGetValue(userId, out var preference) && !IsEnabled(preference, type))
            {
                continue;
            }

            db.Notifications.Add(new Notification
            {
                BusinessId = businessId,
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                Link = link,
            });
        }
    }

    private static bool IsEnabled(NotificationPreference preference, string type) => type switch
    {
        "JoinRequestSubmitted" => preference.NotifyOnJoinRequest,
        "LowStock" => preference.NotifyOnLowStock,
        _ => true,
    };
}
