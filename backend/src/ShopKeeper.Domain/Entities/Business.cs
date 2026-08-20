namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;
using ShopKeeper.Domain.Enums;

/// <summary>
/// The tenant root. Every business-owned record ultimately traces back to a Business
/// via BusinessId. Nothing outside this graph should ever be joined across businesses.
/// </summary>
public class Business : BaseEntity
{
    public string Name { get; set; } = default!;
    public string? LegalName { get; set; }
    public BusinessType BusinessType { get; set; }
    public string Country { get; set; } = default!;
    public string CurrencyCode { get; set; } = "GHS";
    public string? LogoUrl { get; set; }
    public string TimeZone { get; set; } = "Africa/Accra";

    public bool IsActive { get; set; } = true;

    /// <summary>Self-serve when Paystack isn't configured (see PlanLimits) - once it is, only
    /// Free is settable directly; paid tiers require a completed Paystack checkout.</summary>
    public PlanTier PlanTier { get; set; } = PlanTier.Free;

    /// <summary>Separate free/self-serve add-on toggle, deliberately not part of Paystack billing yet -
    /// Paystack subscriptions are locked to one fixed price per plan, so a real paid toggle would need
    /// cancel-and-recreate-subscription logic not worth building alongside the initial billing integration.</summary>
    public bool HasUnlimitedInventoryAddOn { get; set; }

    public string? PaystackCustomerCode { get; set; }
    public string? PaystackSubscriptionCode { get; set; }

    /// <summary>Required (alongside the subscription code) to disable/cancel via Paystack's API - obtained
    /// when the subscription is created/fetched, distinct from the account's secret key.</summary>
    public string? PaystackSubscriptionEmailToken { get; set; }

    /// <summary>Paystack's own status string stored verbatim ("active", "non-renewing", "attention",
    /// "completed", "cancelled", ...) rather than converted to a local enum, so a new Paystack status
    /// value never breaks a conversion - it just displays as-is.</summary>
    public string? PaystackSubscriptionStatus { get; set; }

    /// <summary>Which PLN_ code the active subscription is on - lets a future admin diff config drift
    /// against the current Paystack:*PlanCode settings.</summary>
    public string? PaystackSubscriptionPlanCode { get; set; }
    public DateTimeOffset? PaystackCurrentPeriodEnd { get; set; }

    public bool OnboardingCompleted { get; set; }
    public int OnboardingStep { get; set; }
    public DateTimeOffset? OnboardingCompletedAt { get; set; }

    public ICollection<BusinessUser> BusinessUsers { get; set; } = new List<BusinessUser>();
    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
    public ICollection<Role> Roles { get; set; } = new List<Role>();
    public BusinessSetting? Setting { get; set; }
}
