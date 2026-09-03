namespace ShopKeeper.Infrastructure.Identity;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ShopKeeper.Application.Common.Interfaces;

public class JwtTokenService(IOptions<JwtSettings> options) : IJwtTokenService
{
    private readonly JwtSettings _settings = options.Value;
    private const int AccessTokenLifetimeMinutes = 15;

    public string GenerateAccessToken(JwtClaims claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, claims.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, claims.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("is_owner", claims.IsOwner ? "true" : "false"),
        };

        if (claims.BusinessId.HasValue)
        {
            tokenClaims.Add(new Claim("business_id", claims.BusinessId.Value.ToString()));
        }

        if (claims.BranchId.HasValue)
        {
            tokenClaims.Add(new Claim("branch_id", claims.BranchId.Value.ToString()));
        }

        tokenClaims.AddRange(claims.Permissions.Select(p => new Claim("permission", p)));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: tokenClaims,
            expires: DateTime.UtcNow.AddMinutes(AccessTokenLifetimeMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshTokenValue()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private const int TwoFactorChallengeLifetimeMinutes = 5;
    private const string TwoFactorChallengePurpose = "2fa_challenge";

    public string GenerateTwoFactorChallengeToken(Guid userId, Guid? businessId, bool rememberMe)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("purpose", TwoFactorChallengePurpose),
            new("remember_me", rememberMe ? "true" : "false"),
        };

        if (businessId.HasValue)
        {
            claims.Add(new Claim("business_id", businessId.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(TwoFactorChallengeLifetimeMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (Guid UserId, Guid? BusinessId, bool RememberMe)? ValidateTwoFactorChallengeToken(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        // Without this, the handler remaps "sub" -> ClaimTypes.NameIdentifier (same gotcha as
        // the JwtBearer options in Program.cs), so the FindFirst(Sub) lookup below finds
        // nothing and every valid token gets misreported as expired/invalid.
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            }, out _);

            var purpose = principal.FindFirst("purpose")?.Value;
            var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (purpose != TwoFactorChallengePurpose || sub is null)
            {
                return null;
            }

            var businessIdClaim = principal.FindFirst("business_id")?.Value;
            var businessId = businessIdClaim is null ? (Guid?)null : Guid.Parse(businessIdClaim);
            var rememberMe = principal.FindFirst("remember_me")?.Value == "true";

            return (Guid.Parse(sub), businessId, rememberMe);
        }
        catch
        {
            return null;
        }
    }
}
