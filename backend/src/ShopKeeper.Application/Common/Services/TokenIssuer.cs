namespace ShopKeeper.Application.Common.Services;

using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Auth.Dtos;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Entities;

/// <summary>
/// Builds an access + refresh token pair for a user, optionally scoped to one of their
/// businesses. Centralized here so every auth entry point (register, login, refresh,
/// onboarding completion) issues tokens the same way.
/// </summary>
public class TokenIssuer(IAppDbContext db, IJwtTokenService jwt)
{
    private const int AccessTokenLifetimeMinutes = 15;
    private const int RefreshTokenLifetimeDays = 30;

    public async Task<AuthResultDto> IssueAsync(
        User user, Guid? activeBusinessId, string? ipAddress, string? userAgent, CancellationToken ct)
    {
        // Status != Removed here is what actually enforces "a removed employee can't get back
        // in": a fresh call to IssueAsync (login, refresh, switch-business) can never mint a
        // token scoped to a business the user was removed from. It does NOT revoke a token
        // already issued before removal - that access token stays valid for the remainder of
        // its ~15-minute lifetime (AccessTokenLifetimeMinutes below). Accepted, documented
        // tradeoff rather than building real-time token revocation: the removed user cannot
        // obtain a *new* token for that business, which is the property that actually matters.
        var memberships = await db.BusinessUsers
            .IgnoreQueryFilters()
            .Where(bu => bu.UserId == user.Id && bu.Status != Domain.Enums.BusinessUserStatus.Removed)
            .Include(bu => bu.Business)
            .Include(bu => bu.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .ToListAsync(ct);

        var active = activeBusinessId.HasValue
            ? memberships.FirstOrDefault(m => m.BusinessId == activeBusinessId.Value)
            : memberships.Count == 1 ? memberships[0] : null;

        var permissions = active?.Role.RolePermissions.Select(rp => rp.Permission.Key).ToArray() ?? [];

        var claims = new JwtClaims(
            UserId: user.Id,
            Email: user.Email,
            BusinessId: active?.BusinessId,
            BranchId: active?.BranchId,
            Permissions: permissions,
            IsOwner: active?.IsOwner ?? false);

        var accessToken = jwt.GenerateAccessToken(claims);
        var refreshTokenValue = jwt.GenerateRefreshTokenValue();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            ActiveBusinessId = active?.BusinessId,
            TokenHash = jwt.Hash(refreshTokenValue),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(RefreshTokenLifetimeDays),
            CreatedByIp = ipAddress,
            UserAgent = userAgent,
        });
        await db.SaveChangesAsync(ct);

        var userDto = new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.IsEmailVerified,
            memberships.Select(m => new UserBusinessDto(
                m.BusinessId, m.Business.Name, m.Role.Name, m.IsOwner, m.Business.OnboardingCompleted)).ToList());

        return new AuthResultDto(
            accessToken,
            refreshTokenValue,
            DateTimeOffset.UtcNow.AddMinutes(AccessTokenLifetimeMinutes),
            userDto);
    }
}
