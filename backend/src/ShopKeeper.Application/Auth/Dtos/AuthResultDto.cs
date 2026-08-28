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
