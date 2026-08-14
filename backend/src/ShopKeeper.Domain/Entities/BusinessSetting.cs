namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

/// <summary>
/// One-to-one extended configuration for a Business, captured during onboarding.
/// Kept separate from Business itself so the tenant root stays lean and this can
/// grow (tax rules, goals, etc.) without churning the core entity.
/// </summary>
public class BusinessSetting : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public bool TaxEnabled { get; set; }
    public string? TaxIdNumber { get; set; }
    public decimal TaxRatePercent { get; set; }
    public bool TaxInclusivePricing { get; set; } = true;

    public int FiscalYearStartMonth { get; set; } = 1;

    /// <summary>Comma-separated BusinessGoal enum values selected during onboarding.</summary>
    public string? Goals { get; set; }

    /// <summary>Opaque code (works as both a scannable QR payload and a typed PIN) that lets a
    /// worker submit a self-service JoinRequest instead of the owner sending a targeted email
    /// invite. Null means no active code. Regenerating overwrites this directly - no history
    /// is kept, the old code just stops working immediately.</summary>
    public string? JoinCode { get; set; }
}
