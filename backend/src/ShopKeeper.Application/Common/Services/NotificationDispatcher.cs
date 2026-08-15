namespace ShopKeeper.Application.Common.Services;

using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

/// <summary>
/// Fans a business-wide event out into one Notification row per recipient. Callers add the
/// rows to the tracked change set but don't save - the caller's own existing SaveChangesAsync
/// (already part of the handler's flow) persists the notification(s) together with whatever
/// triggered them, so e.g. a JoinRequest and the owner's notification about it either both
/// commit or neither does.
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

        foreach (var userId in ownerUserIds)
        {
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
}
