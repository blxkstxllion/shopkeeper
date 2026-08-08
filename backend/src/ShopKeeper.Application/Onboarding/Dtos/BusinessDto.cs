namespace ShopKeeper.Application.Onboarding.Dtos;

public record BusinessDto(
    Guid Id,
    string Name,
    string BusinessType,
    string Country,
    string CurrencyCode,
    string? LogoUrl,
    bool OnboardingCompleted,
    Guid FirstBranchId,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt);
