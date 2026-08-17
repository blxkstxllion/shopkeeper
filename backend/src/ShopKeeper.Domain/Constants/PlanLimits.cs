namespace ShopKeeper.Domain.Constants;

using ShopKeeper.Domain.Enums;

/// <summary>Branch/product/staff caps and feature access are hardcoded per tier here, not stored
/// in a DB table - there's no real billing or admin-editable pricing yet (see PlanTier), so a
/// static lookup is the honest representation of what actually exists today. Revisit once real
/// billing lands and plans need to be configurable.</summary>
public record PlanLimitInfo(int MaxBranches, int MaxProducts, int MaxStaff, bool HasReports, bool HasAi);

public static class PlanLimits
{
    public static PlanLimitInfo For(PlanTier tier) => tier switch
    {
        PlanTier.Free => new PlanLimitInfo(2, 25, 2, false, false),
        PlanTier.Business => new PlanLimitInfo(5, 250, 10, true, false),
        PlanTier.BusinessAi => new PlanLimitInfo(5, 250, 10, true, true),
        PlanTier.Enterprise => new PlanLimitInfo(int.MaxValue, 2000, int.MaxValue, true, false),
        PlanTier.EnterpriseAi => new PlanLimitInfo(int.MaxValue, 2000, int.MaxValue, true, true),
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown plan tier."),
    };
}
