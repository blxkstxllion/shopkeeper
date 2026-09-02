namespace ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Abstraction over outbound transactional email. The Phase 1 implementation logs
/// messages instead of delivering them - swap in a real provider (SendGrid, SES, etc.)
/// in Infrastructure without touching any Application code.
/// </summary>
public interface IEmailSender
{
    Task SendEmailVerificationAsync(string toEmail, string firstName, string verificationToken, CancellationToken ct = default);

    Task SendPasswordResetAsync(string toEmail, string firstName, string resetToken, CancellationToken ct = default);

    Task SendBusinessInviteAsync(
        string toEmail, string businessName, string inviterName, string inviteToken, CancellationToken ct = default);

    /// <summary>Delivers a scheduled report's generated document (see ScheduledReportRunner) as
    /// an email attachment - the one email type here that isn't a plain templated link, since
    /// the whole point is the recipient gets the file without visiting the app at all.</summary>
    Task SendReportEmailAsync(
        string toEmail, string businessName, byte[] attachment, string attachmentFileName, string contentType,
        CancellationToken ct = default);
}
