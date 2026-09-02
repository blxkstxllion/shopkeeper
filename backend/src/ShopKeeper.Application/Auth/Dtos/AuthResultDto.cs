namespace ShopKeeper.Application.Auth.Dtos;

public record AuthResultDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    UserDto User);

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsEmailVerified,
    // True only for accounts created after email verification enforcement shipped (see
    // User.EmailVerificationEnforced) and still unverified - the frontend uses this, not
    // IsEmailVerified alone, to decide whether to block the app. Existing accounts from
    // before this shipped stay usable regardless of verification status.
    bool MustVerifyEmail,
    string? PhotoUrl,
    IReadOnlyList<UserBusinessDto> Businesses);

public record UserBusinessDto(
    Guid BusinessId,
    string BusinessName,
    string RoleName,
    bool IsOwner,
    bool OnboardingCompleted,
    string CurrencyCode,
    string ColorTheme);
