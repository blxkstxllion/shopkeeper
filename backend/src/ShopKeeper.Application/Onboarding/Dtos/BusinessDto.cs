namespace ShopKeeper.Application.Onboarding.Dtos;

using ShopKeeper.Application.Auth.Dtos;

public record BusinessDto(
    Guid Id,
    string Name,
    string BusinessType,
    string? BusinessTypeOther,
    string Country,
    string CurrencyCode,
    string? LogoUrl,
    string ColorTheme,
    bool OnboardingCompleted,
    Guid FirstBranchId,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    UserDto User);
