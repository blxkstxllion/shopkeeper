namespace ShopKeeper.Application.Notifications.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Entities;

public record MarkNotificationReadCommand(Guid NotificationId) : IRequest;

public class MarkNotificationReadCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<MarkNotificationReadCommand>
{
    public async Task Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        // Scoped to this user's own notification, not just the tenant - one employee must not
        // be able to mark another employee's notification as read (or even confirm it exists).
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId && n.UserId == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(Notification), request.NotificationId);

        notification.ReadAt ??= DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }
}
