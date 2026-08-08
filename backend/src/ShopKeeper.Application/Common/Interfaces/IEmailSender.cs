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
}
