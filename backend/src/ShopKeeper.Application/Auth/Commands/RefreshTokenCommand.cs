namespace ShopKeeper.Application.Auth.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Auth.Dtos;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Common.Services;

public record RefreshTokenCommand(string RefreshToken, string? IpAddress, string? UserAgent = null) : IRequest<AuthResultDto>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public class RefreshTokenCommandHandler(IAppDbContext db, IJwtTokenService jwt, TokenIssuer tokenIssuer)
    : IRequestHandler<RefreshTokenCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var hash = jwt.Hash(request.RefreshToken);
        var existing = await db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash, cancellationToken);

        if (existing is null)
        {
            throw new AuthenticationException("Invalid refresh token.");
        }

        if (existing.IsRevoked)
        {
            // Reuse of a rotated-out token indicates possible theft: revoke the whole chain.
            await RevokeDescendantsAsync(existing.UserId, cancellationToken);
            throw new AuthenticationException("This refresh token has already been used and was revoked.");
        }

        if (existing.IsExpired)
        {
            throw new AuthenticationException("Refresh token has expired. Please log in again.");
        }

        var result = await tokenIssuer.IssueAsync(existing.User, existing.ActiveBusinessId, request.IpAddress, request.UserAgent, cancellationToken);

        existing.RevokedAt = DateTimeOffset.UtcNow;
        existing.RevokedByIp = request.IpAddress;
        existing.ReasonRevoked = "Rotated on refresh";
        await db.SaveChangesAsync(cancellationToken);

        return result;
    }

    private async Task RevokeDescendantsAsync(Guid userId, CancellationToken ct)
    {
        var active = await db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in active)
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
            token.ReasonRevoked = "Possible token theft detected";
        }

        await db.SaveChangesAsync(ct);
    }
}
