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
        var (accessToken, userDto, resolvedBusinessId) = await BuildAccessTokenAsync(user, activeBusinessId, ct);
        var refreshTokenValue = jwt.GenerateRefreshTokenValue();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            ActiveBusinessId = resolvedBusinessId,
            TokenHash = jwt.Hash(refreshTokenValue),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(RefreshTokenLifetimeDays),
            CreatedByIp = ipAddress,
            UserAgent = userAgent,
        });
        await db.SaveChangesAsync(ct);

        return new AuthResultDto(
            accessToken,
            refreshTokenValue,
            DateTimeOffset.UtcNow.AddMinutes(AccessTokenLifetimeMinutes),
            userDto);
    }

    /// <summary>
    /// Used only for the refresh-token grace-period race (RefreshTokenCommandHandler): when a
    /// second, near-simultaneous refresh request presents a token that a first request already
    /// rotated, the caller already has a valid successor RefreshToken - this hands back a fresh
    /// access token for the same session without creating a second RefreshToken row.
    /// </summary>
    public async Task<AuthResultDto> IssueAccessTokenOnlyAsync(User user, Guid? activeBusinessId, CancellationToken ct)
    {
        var (accessToken, userDto, _) = await BuildAccessTokenAsync(user, activeBusinessId, ct);
        return new AuthResultDto(accessToken, string.Empty, DateTimeOffset.UtcNow.AddMinutes(AccessTokenLifetimeMinutes), userDto);
    }

    private async Task<(string AccessToken, UserDto UserDto, Guid? ResolvedBusinessId)> BuildAccessTokenAsync(
        User user, Guid? activeBusinessId, CancellationToken ct)
    {
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

        var userDto = new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.IsEmailVerified,
            memberships.Select(m => new UserBusinessDto(
                m.BusinessId, m.Business.Name, m.Role.Name, m.IsOwner, m.Business.OnboardingCompleted)).ToList());

        return (accessToken, userDto, active?.BusinessId);
    }
}
