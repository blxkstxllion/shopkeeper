namespace ShopKeeper.Infrastructure.Payments;

public class PaystackSettings
{
    public const string SectionName = "Paystack";

    /// <summary>Required for PaystackClient to be registered - see DependencyInjection.AddInfrastructure.</summary>
    public string SecretKey { get; set; } = default!;

    /// <summary>PLN_ codes created by hand in the Paystack dashboard - one per paid tier. Plans
    /// essentially never change, so there's no bootstrap/create-on-first-use code path here.</summary>
    public string BusinessPlanCode { get; set; } = default!;
    public string BusinessAiPlanCode { get; set; } = default!;
    public string EnterprisePlanCode { get; set; } = default!;
    public string EnterpriseAiPlanCode { get; set; } = default!;

    /// <summary>Base URL of the deployed frontend (no trailing slash), used to build the checkout
    /// callback_url ("{FrontendBaseUrl}/app/billing/callback"). Its own copy rather than sharing
    /// EmailSettings.FrontendBaseUrl - every settings POCO here is independently bound from its
    /// own config section already.</summary>
    public string FrontendBaseUrl { get; set; } = default!;
}
