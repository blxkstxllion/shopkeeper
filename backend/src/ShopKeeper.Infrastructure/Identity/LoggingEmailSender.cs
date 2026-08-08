namespace ShopKeeper.Infrastructure.Identity;

using Microsoft.Extensions.Logging;
using ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Development-only stand-in for a real transactional email provider. Logs the message
/// instead of delivering it. Replace with a SendGrid/SES/etc. implementation before
/// production - do not ship this class as-is.
/// </summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendEmailVerificationAsync(string toEmail, string firstName, string verificationToken, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[DEV EMAIL] Verification email to {Email} ({FirstName}). Token: {Token}",
            toEmail, firstName, verificationToken);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string firstName, string resetToken, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[DEV EMAIL] Password reset email to {Email} ({FirstName}). Token: {Token}",
            toEmail, firstName, resetToken);
        return Task.CompletedTask;
    }
}
