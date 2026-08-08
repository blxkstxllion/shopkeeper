namespace ShopKeeper.Application.Common.Interfaces;

public record JwtClaims(Guid UserId, string Email, Guid? BusinessId, Guid? BranchId, string[] Permissions, bool IsOwner);

public interface IJwtTokenService
{
    string GenerateAccessToken(JwtClaims claims);

    /// <summary>Returns the raw (plaintext) refresh token. Callers must hash it before persisting.</summary>
    string GenerateRefreshTokenValue();

    string Hash(string value);
}
