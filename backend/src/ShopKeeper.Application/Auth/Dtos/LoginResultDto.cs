namespace ShopKeeper.Application.Auth.Dtos;

/// <summary>
/// Either a completed login (Auth populated, RequiresTwoFactor false) or a request for the
/// second factor (ChallengeToken populated, Auth null - no tokens are issued until the code
/// is verified via VerifyTwoFactorCommand).
/// </summary>
public record LoginResultDto(bool RequiresTwoFactor, string? ChallengeToken, AuthResultDto? Auth);
