namespace ShopKeeper.Application.Auth.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Auth.Dtos;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Domain.Entities;

public record RefreshTokenCommand(string RefreshToken, string? IpAddress, string? UserAgent = null) : IRequest<AuthResultDto>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public class RefreshTokenCommandHandler(IAppDbContext db, IJwtTokenService jwt, TokenIssuer tokenIssuer)
    : IRequestHandler<RefreshTokenCommand, AuthResultDto>
{
    // How long a just-rotated token's reuse is treated as a legitimate second request (e.g. two
    // browser tabs firing refresh at nearly the same moment) rather than theft. Long enough to
    // absorb realistic network/response jitter between two requests racing on the same cookie,
    // short enough that it doesn't meaningfully widen the window a truly stolen token is usable.
    private static readonly TimeSpan ReuseGracePeriod = TimeSpan.FromSeconds(30);

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
            if (await TryHandleLegitimateRaceAsync(existing, cancellationToken) is { } raceResult)
            {
                return raceResult;
            }

            // Outside the grace period, or the rotation chain is broken (no successor, or the
            // successor is itself already revoked) - genuine reuse of a dead token: theft.
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

        var successorHash = jwt.Hash(result.RefreshToken);
        var successor = await db.RefreshTokens.FirstAsync(rt => rt.TokenHash == successorHash, cancellationToken);
        existing.ReplacedByTokenId = successor.Id;

        await db.SaveChangesAsync(cancellationToken);

        return result;
    }

    /// <summary>
    /// A second request presenting a token that a first request already rotated moments ago -
    /// two tabs, the same cookie, near-simultaneous refresh. Not theft: the successor token
    /// exists and is still active, so this hands back a fresh access token for the same session
    /// instead of cascading a revocation across every device. Returns null when the reuse
    /// doesn't qualify (no successor, successor already dead, or outside the grace window), so
    /// the caller falls through to the existing theft response.
    /// </summary>
    private async Task<AuthResultDto?> TryHandleLegitimateRaceAsync(RefreshToken existing, CancellationToken ct)
    {
        if (existing.ReplacedByTokenId is not { } successorId
            || DateTimeOffset.UtcNow - existing.RevokedAt!.Value >= ReuseGracePeriod)
        {
            return null;
        }

        var successor = await db.RefreshTokens.FirstOrDefaultAsync(rt => rt.Id == successorId, ct);
        if (successor is null || !successor.IsActive)
        {
            return null;
        }

        return await tokenIssuer.IssueAccessTokenOnlyAsync(existing.User, existing.ActiveBusinessId, ct);
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
