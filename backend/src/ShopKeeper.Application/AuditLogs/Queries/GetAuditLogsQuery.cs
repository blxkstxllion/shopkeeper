namespace ShopKeeper.Application.AuditLogs.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.AuditLogs.Dtos;
using ShopKeeper.Application.Common.Dtos;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;

public record GetAuditLogsQuery(
    string? EntityType,
    string? Action,
    Guid? UserId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize) : IRequest<PagedResult<AuditLogDto>>;

public class GetAuditLogsQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetAuditLogsQuery, PagedResult<AuditLogDto>>
{
    public async Task<PagedResult<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.AuditLogsView);
        var businessId = currentUser.RequireBusinessId();

        // AuditLog is not an ITenantEntity (BusinessId is nullable, to allow logging
        // platform-level events before a business exists) - so it's exempt from
        // AppDbContext's automatic tenant query filter and must be scoped by hand here.
        var query = db.AuditLogs.Where(log => log.BusinessId == businessId);

        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            query = query.Where(log => log.EntityType == request.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            query = query.Where(log => log.Action == request.Action);
        }

        if (request.UserId.HasValue)
        {
            query = query.Where(log => log.UserId == request.UserId);
        }

        if (request.From.HasValue)
        {
            query = query.Where(log => log.CreatedAt >= request.From);
        }

        if (request.To.HasValue)
        {
            query = query.Where(log => log.CreatedAt <= request.To);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        // Ordering and paging happen client-side, after the (server-side, indexed) filter above -
        // EF Core's SQLite provider has no native ORDER BY support for DateTimeOffset columns
        // (throws NotSupportedException), only Postgres does. Audit-log volume per business is
        // not expected to be large enough for this to matter; if that changes, the fix is a
        // provider-specific DateTimeOffset value converter, not per-query workarounds.
        var filtered = await query.ToListAsync(cancellationToken);
        var rows = filtered.OrderByDescending(log => log.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var actorIds = rows.Where(r => r.UserId.HasValue).Select(r => r.UserId!.Value).Distinct().ToList();
        var actorNames = await db.Users.Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}", cancellationToken);

        var items = rows.Select(log => new AuditLogDto(
            log.Id,
            log.Action,
            log.EntityType,
            log.EntityId,
            log.UserId.HasValue && actorNames.TryGetValue(log.UserId.Value, out var name) ? name : null,
            log.PreviousValue,
            log.NewValue,
            log.IpAddress,
            log.CreatedAt)).ToList();

        return new PagedResult<AuditLogDto>(items, totalCount, page, pageSize);
    }
}
