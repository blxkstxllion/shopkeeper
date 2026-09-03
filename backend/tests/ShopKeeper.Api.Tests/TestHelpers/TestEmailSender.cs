namespace ShopKeeper.Api.Tests.TestHelpers;

using ShopKeeper.Application.Common.Interfaces;

/// <summary>Records the last email of each kind "sent" so tests can assert on it, instead of
/// delivering anything - mirrors LoggingEmailSender's role but captures state for assertions.</summary>
public class TestEmailSender : IEmailSender
{
    public (string ToEmail, string BusinessName, string InviterName, string InviteToken)? LastInvite { get; private set; }

    public (string ToEmail, string FirstName, string VerificationToken)? LastVerification { get; private set; }

    public (string ToEmail, string BusinessName, byte[] Attachment, string AttachmentFileName, string ContentType)? LastReportEmail { get; private set; }

    public Task SendEmailVerificationAsync(string toEmail, string firstName, string verificationToken, CancellationToken ct = default)
    {
        LastVerification = (toEmail, firstName, verificationToken);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string firstName, string resetToken, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task SendBusinessInviteAsync(
        string toEmail, string businessName, string inviterName, string inviteToken, CancellationToken ct = default)
    {
        LastInvite = (toEmail, businessName, inviterName, inviteToken);
        return Task.CompletedTask;
    }

    public Task SendReportEmailAsync(
        string toEmail, string businessName, byte[] attachment, string attachmentFileName, string contentType,
        CancellationToken ct = default)
    {
        LastReportEmail = (toEmail, businessName, attachment, attachmentFileName, contentType);
        return Task.CompletedTask;
    }
}
