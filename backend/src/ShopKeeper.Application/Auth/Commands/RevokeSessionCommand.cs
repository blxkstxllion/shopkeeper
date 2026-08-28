namespace ShopKeeper.Application.Auth.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Entities;

/// <summary>Signs out one other device/browser (e.g. a lost phone) without touching the caller's own session.</summary>
public record RevokeSessionCommand(Guid SessionId) : IRequest;

public class RevokeSessionCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<RevokeSessionCommand>
{
    public async Task Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var session = await db.RefreshTokens.FirstOrDefaultAsync(rt => rt.Id == request.SessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(RefreshToken), request.SessionId);

        if (session.UserId != userId)
        {
            // Reported as NotFound, not Forbidden, so this endpoint can't be used to probe
            // whether a given session id belongs to someone else.
            throw new NotFoundException(nameof(RefreshToken), request.SessionId);
        }

        if (session.RevokedAt is null)
        {
            session.RevokedAt = DateTimeOffset.UtcNow;
            session.ReasonRevoked = "Revoked by user from active sessions list";
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
