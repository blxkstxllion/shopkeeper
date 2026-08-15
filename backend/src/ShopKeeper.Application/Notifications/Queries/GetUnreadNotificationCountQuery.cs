namespace ShopKeeper.Application.Notifications.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;

/// <summary>Deliberately just a count, not the notifications themselves - this is the endpoint
/// the frontend polls every ~30s for the bell badge, so it stays cheap.</summary>
public record GetUnreadNotificationCountQuery : IRequest<int>;

public class GetUnreadNotificationCountQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetUnreadNotificationCountQuery, int>
{
    public Task<int> Handle(GetUnreadNotificationCountQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        return db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, cancellationToken);
    }
}
