namespace ShopKeeper.Application.Common.Interfaces;

public record JwtClaims(Guid UserId, string Email, Guid? BusinessId, Guid? BranchId, string[] Permissions, bool IsOwner);

public interface IJwtTokenService
{
    string GenerateAccessToken(JwtClaims claims);

    /// <summary>Returns the raw (plaintext) refresh token. Callers must hash it before persisting.</summary>
    string GenerateRefreshTokenValue();

    string Hash(string value);

    /// <summary>
    /// A short-lived (5 minute), single-purpose token identifying "this user passed their
    /// password check and is mid-way through 2FA" - deliberately NOT a real access token
    /// (carries no permission claims, can't authorize any API call) so a login that stops
    /// after the password step can never be mistaken for a completed one. Carries the
    /// originally-requested businessId through so a multi-business login doesn't lose that
    /// choice while the user is off entering their code. Also carries the "Keep me signed in"
    /// choice through, since tokens aren't issued (and the cookie isn't set) until the code is verified.
    /// </summary>
    string GenerateTwoFactorChallengeToken(Guid userId, Guid? businessId, bool rememberMe);

    /// <summary>Returns the (userId, businessId, rememberMe) if `token` is a valid, unexpired two-factor challenge token; otherwise null.</summary>
    (Guid UserId, Guid? BusinessId, bool RememberMe)? ValidateTwoFactorChallengeToken(string token);
}
