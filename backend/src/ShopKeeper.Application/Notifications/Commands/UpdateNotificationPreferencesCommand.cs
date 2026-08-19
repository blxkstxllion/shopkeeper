namespace ShopKeeper.Application.Notifications.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Entities;

public record UpdateNotificationPreferencesCommand(bool NotifyOnJoinRequest, bool NotifyOnLowStock) : IRequest;

public class UpdateNotificationPreferencesCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<UpdateNotificationPreferencesCommand>
{
    public async Task Handle(UpdateNotificationPreferencesCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var businessId = currentUser.RequireBusinessId();

        var preference = await db.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (preference is null)
        {
            preference = new NotificationPreference { BusinessId = businessId, UserId = userId };
            db.NotificationPreferences.Add(preference);
        }

        preference.NotifyOnJoinRequest = request.NotifyOnJoinRequest;
        preference.NotifyOnLowStock = request.NotifyOnLowStock;

        await db.SaveChangesAsync(cancellationToken);
    }
}
