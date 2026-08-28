namespace ShopKeeper.Application.Notifications.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;

public record MarkAllNotificationsReadCommand : IRequest;

public class MarkAllNotificationsReadCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<MarkAllNotificationsReadCommand>
{
    public async Task Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var now = DateTimeOffset.UtcNow;

        var unread = await db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null).ToListAsync(cancellationToken);
        foreach (var notification in unread)
        {
            notification.ReadAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
