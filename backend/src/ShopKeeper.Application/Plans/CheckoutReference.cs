namespace ShopKeeper.Application.Plans;

using ShopKeeper.Domain.Enums;

/// <summary>
/// Builds and parses the self-supplied Paystack transaction reference ("chk_{businessId:N}_{tier}_{nonce}").
/// This app fully controls the reference's shape, so business/tier are recovered by parsing this
/// string rather than trusting an unconfirmed nested field in Paystack's verify/webhook responses.
/// </summary>
public static class CheckoutReference
{
    private const string Prefix = "chk";

    public static string Build(Guid businessId, PlanTier tier) => $"{Prefix}_{businessId:N}_{tier}_{Guid.NewGuid():N}";

    public static bool TryParse(string? reference, out Guid businessId, out PlanTier tier)
    {
        businessId = Guid.Empty;
        tier = default;

        if (string.IsNullOrEmpty(reference))
        {
            return false;
        }

        var parts = reference.Split('_');
        return parts.Length == 4
            && parts[0] == Prefix
            && Guid.TryParseExact(parts[1], "N", out businessId)
            && Enum.TryParse(parts[2], out tier);
    }
}
