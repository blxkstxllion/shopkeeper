namespace ShopKeeper.Infrastructure.Identity;

public class EmailSettings
{
    public const string SectionName = "Email";

    /// <summary>Verified SES sender address (e.g. "no-reply@yourdomain.com"). Required for SesEmailSender to be registered - see DependencyInjection.AddInfrastructure.</summary>
    public string FromAddress { get; set; } = default!;

    public string FromName { get; set; } = "The Shop Keeper";

    /// <summary>AWS region the sending SES identity was verified in (e.g. "us-east-1").</summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>Base URL of the deployed frontend (no trailing slash), used to build links in email bodies - e.g. "https://app.example.com".</summary>
    public string FrontendBaseUrl { get; set; } = default!;
}
