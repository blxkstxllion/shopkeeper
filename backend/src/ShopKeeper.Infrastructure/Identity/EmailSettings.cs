namespace ShopKeeper.Infrastructure.Identity;

public class EmailSettings
{
    public const string SectionName = "Email";

    /// <summary>Verified SES sender address (e.g. "no-reply@yourdomain.com"). Required for SesEmailSender to be registered - see DependencyInjection.AddInfrastructure.</summary>
    public string FromAddress { get; set; } = default!;

    public string FromName { get; set; } = "The Shop Keeper";

    /// <summary>AWS region the sending SES identity was verified in (e.g. "us-east-1") - unused
    /// when ResendApiKey is set, since Resend has no region concept.</summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>Resend API key (see resend.com) - when set, DependencyInjection registers
    /// ResendEmailSender instead of SesEmailSender, regardless of whether FromAddress/Region
    /// are also configured. Not itself read from this class (DependencyInjection reads
    /// Email:ResendApiKey directly to build the HttpClient's auth header before this options
    /// object even exists) - present here only so its intent is documented alongside the rest
    /// of the email settings.</summary>
    public string? ResendApiKey { get; set; }

    /// <summary>Base URL of the deployed frontend (no trailing slash), used to build links in email bodies - e.g. "https://app.example.com".</summary>
    public string FrontendBaseUrl { get; set; } = default!;
}
