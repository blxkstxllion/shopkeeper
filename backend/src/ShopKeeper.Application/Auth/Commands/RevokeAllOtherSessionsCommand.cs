namespace ShopKeeper.Application.Auth.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;

/// <summary>"Sign out everywhere else" - keeps the caller's own current session alive.</summary>
public record RevokeAllOtherSessionsCommand(string? CurrentRefreshToken) : IRequest;

public class RevokeAllOtherSessionsCommandHandler(IAppDbContext db, ICurrentUserService currentUser, IJwtTokenService jwt)
    : IRequestHandler<RevokeAllOtherSessionsCommand>
{
    public async Task Handle(RevokeAllOtherSessionsCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var currentHash = request.CurrentRefreshToken is null ? null : jwt.Hash(request.CurrentRefreshToken);

        var others = await db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.TokenHash != currentHash)
            .ToListAsync(cancellationToken);

        foreach (var session in others)
        {
            session.RevokedAt = DateTimeOffset.UtcNow;
            session.ReasonRevoked = "Revoked by user via 'sign out of all other sessions'";
        }

        if (others.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
