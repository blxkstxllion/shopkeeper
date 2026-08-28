namespace ShopKeeper.Application.Plans.Dtos;

using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Enums;

public record PlanUsageDto(
    PlanTier CurrentTier,
    PlanLimitInfo Limits,
    int BranchCount,
    int ProductCount,
    int StaffCount,
    bool HasUnlimitedInventoryAddOn,
    bool BillingEnabled,
    string? SubscriptionStatus,
    DateTimeOffset? CurrentPeriodEnd);

public record CheckoutSessionDto(string AuthorizationUrl);

public record VerifyCheckoutResultDto(bool Success, PlanTier? NewTier);
