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

    public bool OnboardingCompleted { get; set; }
    public int OnboardingStep { get; set; }
    public DateTimeOffset? OnboardingCompletedAt { get; set; }

    public ICollection<BusinessUser> BusinessUsers { get; set; } = new List<BusinessUser>();
    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
    public ICollection<Role> Roles { get; set; } = new List<Role>();
    public BusinessSetting? Setting { get; set; }
}
