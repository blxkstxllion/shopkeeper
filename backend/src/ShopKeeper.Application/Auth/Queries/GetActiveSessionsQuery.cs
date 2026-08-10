namespace ShopKeeper.Application.Auth.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Auth.Dtos;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;

public record GetActiveSessionsQuery(string? CurrentRefreshToken) : IRequest<IReadOnlyList<SessionDto>>;

public class GetActiveSessionsQueryHandler(IAppDbContext db, ICurrentUserService currentUser, IJwtTokenService jwt)
    : IRequestHandler<GetActiveSessionsQuery, IReadOnlyList<SessionDto>>
{
    public async Task<IReadOnlyList<SessionDto>> Handle(GetActiveSessionsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var currentHash = request.CurrentRefreshToken is null ? null : jwt.Hash(request.CurrentRefreshToken);

        // Filtering by ExpiresAt and ordering by CreatedAt both happen client-side: SQLite's
        // provider can't translate DateTimeOffset comparisons or ORDER BY on that type, and
        // per-user session counts are small enough that this is cheap.
        var sessions = await db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        return sessions
            .Where(rt => rt.ExpiresAt > now)
            .OrderByDescending(rt => rt.CreatedAt)
            .Select(rt => new SessionDto(rt.Id, rt.CreatedAt, rt.ExpiresAt, rt.CreatedByIp, rt.UserAgent, rt.TokenHash == currentHash))
            .ToList();
    }
}
