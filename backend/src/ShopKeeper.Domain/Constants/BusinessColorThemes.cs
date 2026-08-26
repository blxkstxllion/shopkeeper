namespace ShopKeeper.Domain.Constants;

/// <summary>
/// The fixed set of brand color presets a business can pick (Settings, or suggested at
/// onboarding based on BusinessType). Shared so UpdateBusinessProfileCommand and
/// CompleteOnboardingCommand validate against the same allow-list rather than drifting.
/// </summary>
public static class BusinessColorThemes
{
    public const string Green = "green";
    public const string Blue = "blue";
    public const string Red = "red";

    public static readonly string[] All = [Green, Blue, Red];
}
