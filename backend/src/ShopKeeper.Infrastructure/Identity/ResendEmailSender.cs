namespace ShopKeeper.Infrastructure.Identity;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Sends real transactional email via the Resend API (https://resend.com) - a plain REST
/// POST, unlike SES's SDK-based v2 client. Picked as the default over SES because Resend
/// approves low-volume transactional senders same-day with no sandbox review, whereas this
/// account's SES production-access request sat pending (and was once denied outright) for
/// weeks. SES support (SesEmailSender) is left in place, not deleted - see
/// DependencyInjection.AddInfrastructure for the priority order between the two.
///
/// Typed HttpClient (see AddInfrastructure) with its base address and Authorization: Bearer
/// header already set, matching PaystackClient/ClaudeAdvisorNarrator's pattern - this class
/// only builds request bodies, never touches auth or the base URL itself.
/// </summary>
public class ResendEmailSender(HttpClient httpClient, IOptions<EmailSettings> options, ILogger<ResendEmailSender> logger)
    : IEmailSender
{
    private readonly EmailSettings _settings = options.Value;

    public Task SendEmailVerificationAsync(string toEmail, string firstName, string verificationToken, CancellationToken ct = default)
    {
        var link = $"{_settings.FrontendBaseUrl}/verify-email?token={Uri.EscapeDataString(verificationToken)}";
        return SendAsync(
            toEmail,
            "Verify your email address",
            EmailTemplates.Build(
                firstName,
                "Verify your email address to finish setting up your account.",
                "Verify email",
                link),
            ct);
    }

    public Task SendPasswordResetAsync(string toEmail, string firstName, string resetToken, CancellationToken ct = default)
    {
        var link = $"{_settings.FrontendBaseUrl}/reset-password?token={Uri.EscapeDataString(resetToken)}";
        return SendAsync(
            toEmail,
            "Reset your password",
            EmailTemplates.Build(
                firstName,
                "We received a request to reset your password. If you didn't make this request, you can safely ignore this email.",
                "Reset password",
                link),
            ct);
    }

    public Task SendBusinessInviteAsync(
        string toEmail, string businessName, string inviterName, string inviteToken, CancellationToken ct = default)
    {
        var link = $"{_settings.FrontendBaseUrl}/accept-invite?token={Uri.EscapeDataString(inviteToken)}";
        var encodedBusiness = WebUtility.HtmlEncode(businessName);
        var encodedInviter = WebUtility.HtmlEncode(inviterName);
        return SendAsync(
            toEmail,
            $"{inviterName} invited you to join {businessName} on The Shop Keeper",
            EmailTemplates.Build(
                null,
                $"{encodedInviter} invited you to join <strong>{encodedBusiness}</strong> on The Shop Keeper.",
                "Accept invite",
                link),
            ct);
    }

    public async Task SendReportEmailAsync(
        string toEmail, string businessName, byte[] attachment, string attachmentFileName, string contentType,
        CancellationToken ct = default)
    {
        var encodedBusiness = WebUtility.HtmlEncode(businessName);
        var body = EmailTemplates.Build(
            null,
            $"Your scheduled report for <strong>{encodedBusiness}</strong> is attached.",
            "Open The Shop Keeper",
            _settings.FrontendBaseUrl);

        // Unlike SES (Simple content has no attachment support, forcing a hand-built MIME
        // message for this one email type), Resend's single JSON endpoint takes attachments
        // as base64 directly alongside html/text - no separate raw-MIME code path needed.
        await PostAsync(
            new ResendEmailRequest(
                From: $"{_settings.FromName} <{_settings.FromAddress}>",
                To: [toEmail],
                Subject: $"Your {businessName} report",
                Html: body.Html,
                Text: body.Text,
                Attachments: [new ResendAttachment(attachmentFileName, Convert.ToBase64String(attachment), contentType)]),
            toEmail,
            ct);
    }

    private Task SendAsync(string toEmail, string subject, (string Html, string Text) body, CancellationToken ct) =>
        PostAsync(
            new ResendEmailRequest(
                From: $"{_settings.FromName} <{_settings.FromAddress}>",
                To: [toEmail],
                Subject: subject,
                Html: body.Html,
                Text: body.Text,
                Attachments: null),
            toEmail,
            ct);

    private async Task PostAsync(ResendEmailRequest request, string toEmail, CancellationToken ct)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("emails", request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                logger.LogError(
                    "Resend rejected an email to {Email}: {Status} {Body}", toEmail, response.StatusCode, responseBody);
            }
        }
        catch (Exception ex)
        {
            // Same "never fail the caller" contract as SesEmailSender - a delivery failure
            // (bad API key, Resend outage, a typo'd recipient) must never fail the command
            // that triggered it.
            logger.LogError(ex, "Failed to send email to {Email} via Resend", toEmail);
        }
    }

    private record ResendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("attachments")] ResendAttachment[]? Attachments);

    private record ResendAttachment(
        [property: JsonPropertyName("filename")] string Filename,
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("content_type")] string ContentType);
}
