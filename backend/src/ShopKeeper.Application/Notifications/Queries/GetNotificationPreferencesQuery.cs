namespace ShopKeeper.Application.Notifications.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Notifications.Dtos;

public record GetNotificationPreferencesQuery : IRequest<NotificationPreferenceDto>;

public class GetNotificationPreferencesQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetNotificationPreferencesQuery, NotificationPreferenceDto>
{
    public async Task<NotificationPreferenceDto> Handle(GetNotificationPreferencesQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        // Tenant-scoped automatically (NotificationPreference is an ITenantEntity). No row yet
        // means every type is still enabled - that's the default, not a special case to handle.
        var preference = await db.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        return preference is null
            ? new NotificationPreferenceDto(true, true)
            : new NotificationPreferenceDto(preference.NotifyOnJoinRequest, preference.NotifyOnLowStock);
    }
}
