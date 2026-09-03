namespace ShopKeeper.Application.Auth.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Auth.Dtos;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Common.Services;

/// <summary>Re-issues tokens scoped to a different business the user belongs to (business switcher in the top nav).</summary>
public record SwitchBusinessCommand(Guid UserId, Guid BusinessId, string? CurrentRefreshToken, string? IpAddress, string? UserAgent = null)
    : IRequest<AuthResultDto>;

public class SwitchBusinessCommandHandler(IAppDbContext db, IJwtTokenService jwt, TokenIssuer tokenIssuer)
    : IRequestHandler<SwitchBusinessCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(SwitchBusinessCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.User), request.UserId);

        var isMember = await db.BusinessUsers.IgnoreQueryFilters()
            .AnyAsync(bu => bu.UserId == request.UserId && bu.BusinessId == request.BusinessId
                && bu.Status == Domain.Enums.BusinessUserStatus.Active, cancellationToken);

        if (!isMember)
        {
            throw new ForbiddenAccessException("You do not have access to this business.");
        }

        // Carries the current session's "Keep me signed in" choice into the re-issued token -
        // otherwise every business switch would silently downgrade a remembered session to
        // session-only, since there's no separate UI here to ask again.
        var rememberMe = false;
        if (!string.IsNullOrEmpty(request.CurrentRefreshToken))
        {
            var hash = jwt.Hash(request.CurrentRefreshToken);
            rememberMe = await db.RefreshTokens
                .Where(rt => rt.TokenHash == hash)
                .Select(rt => rt.RememberMe)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await tokenIssuer.IssueAsync(user, request.BusinessId, rememberMe, request.IpAddress, request.UserAgent, cancellationToken);
    }
}
