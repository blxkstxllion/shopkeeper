namespace ShopKeeper.Application.Auth.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Interfaces;

public record LogoutCommand(string RefreshToken, string? IpAddress) : IRequest;

public class LogoutCommandHandler(IAppDbContext db, IJwtTokenService jwt) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var hash = jwt.Hash(request.RefreshToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == hash, cancellationToken);

        if (existing is not null && existing.RevokedAt is null)
        {
            existing.RevokedAt = DateTimeOffset.UtcNow;
            existing.RevokedByIp = request.IpAddress;
            existing.ReasonRevoked = "User logged out";
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
