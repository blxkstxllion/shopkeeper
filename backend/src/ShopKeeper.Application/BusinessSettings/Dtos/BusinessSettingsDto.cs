namespace ShopKeeper.Application.BusinessSettings.Dtos;

public record BusinessSettingsDto(
    Guid BusinessId,
    string Name,
    string? LegalName,
    string BusinessType,
    string Country,
    string CurrencyCode,
    string TimeZone,
    string? LogoUrl,
    string ColorTheme,
    bool TaxEnabled,
    string? TaxIdNumber,
    decimal TaxRatePercent,
    bool TaxInclusivePricing);
