namespace ShopKeeper.Application.Notifications.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Dtos;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Notifications.Dtos;

public record GetNotificationsQuery(int Page, int PageSize) : IRequest<PagedResult<NotificationDto>>;

public class GetNotificationsQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetNotificationsQuery, PagedResult<NotificationDto>>
{
    public async Task<PagedResult<NotificationDto>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        // Tenant-scoped automatically (Notification is an ITenantEntity) - narrowed further to
        // this user's own notifications, since a business-wide event fans out into one row per
        // recipient and each recipient should only ever see their own copy.
        var query = db.Notifications.Where(n => n.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        // Ordered client-side after the server-side filter, same reasoning as
        // GetAuditLogsQuery: EF Core's SQLite provider has no native ORDER BY support for
        // DateTimeOffset columns (Postgres does), and per-user notification volume isn't
        // expected to be large enough for this to matter.
        var filtered = await query.ToListAsync(cancellationToken);
        var rows = filtered.OrderByDescending(n => n.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var items = rows.Select(n => new NotificationDto(
            n.Id, n.Type, n.Title, n.Message, n.Link, n.ReadAt.HasValue, n.CreatedAt)).ToList();

        return new PagedResult<NotificationDto>(items, totalCount, page, pageSize);
    }
}
