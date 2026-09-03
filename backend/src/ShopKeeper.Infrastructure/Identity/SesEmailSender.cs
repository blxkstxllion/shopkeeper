namespace ShopKeeper.Infrastructure.Identity;

using System.Net;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Sends real transactional email via AWS SES v2. Credentials are resolved through the
/// standard AWS SDK credential chain (an EC2 instance profile in production - see
/// docs/deployment.md - or AWS_ACCESS_KEY_ID/AWS_SECRET_ACCESS_KEY env vars for local
/// testing) rather than anything stored in this app's own config, so there's no AWS
/// secret to manage as a ShopKeeper secret at all in the recommended deployment. The SES
/// client is injected (see DependencyInjection.AddInfrastructure) rather than constructed
/// here, both to reuse one client instead of one per email and so tests can substitute a
/// fake IAmazonSimpleEmailServiceV2 and assert on the built request.
/// </summary>
public class SesEmailSender(
    IAmazonSimpleEmailServiceV2 client, IOptions<EmailSettings> options, ILogger<SesEmailSender> logger) : IEmailSender
{
    private readonly EmailSettings _settings = options.Value;

    public Task SendEmailVerificationAsync(string toEmail, string firstName, string verificationToken, CancellationToken ct = default)
    {
        var link = $"{_settings.FrontendBaseUrl}/verify-email?token={Uri.EscapeDataString(verificationToken)}";
        return SendAsync(
            toEmail,
            "Verify your email address",
            BuildBody(
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
            BuildBody(
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
            BuildBody(
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
        // SES's "Simple" content (used by every other method here) has no attachment support -
        // only "Raw" (a full RFC 2045 MIME message) does, so this is the one email that needs
        // to actually build MIME instead of handing SES a subject/body pair.
        var encodedBusiness = WebUtility.HtmlEncode(businessName);
        var body = BuildBody(
            null,
            $"Your scheduled report for <strong>{encodedBusiness}</strong> is attached.",
            "Open The Shop Keeper",
            _settings.FrontendBaseUrl);

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse($"{_settings.FromName} <{_settings.FromAddress}>"));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = $"Your {businessName} report";

        var builder = new BodyBuilder { HtmlBody = body.Html, TextBody = body.Text };
        builder.Attachments.Add(attachmentFileName, attachment, ContentType.Parse(contentType));
        message.Body = builder.ToMessageBody();

        using var stream = new MemoryStream();
        await message.WriteToAsync(stream, ct);

        try
        {
            await client.SendEmailAsync(
                new SendEmailRequest
                {
                    Destination = new Destination { ToAddresses = [toEmail] },
                    Content = new EmailContent { Raw = new RawMessage { Data = stream } },
                },
                ct);
        }
        catch (Exception ex)
        {
            // Same "never fail the caller" contract as SendAsync below - a scheduled report
            // that fails to deliver should be retried next cycle, not crash the runner.
            logger.LogError(ex, "Failed to send scheduled report email to {Email} via SES", toEmail);
        }
    }

    private async Task SendAsync(string toEmail, string subject, (string Html, string Text) body, CancellationToken ct)
    {
        var request = new SendEmailRequest
        {
            FromEmailAddress = $"{_settings.FromName} <{_settings.FromAddress}>",
            Destination = new Destination { ToAddresses = [toEmail] },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = subject },
                    Body = new Body
                    {
                        Html = new Content { Data = body.Html },
                        Text = new Content { Data = body.Text },
                    },
                },
            },
        };

        try
        {
            await client.SendEmailAsync(request, ct);
        }
        catch (Exception ex)
        {
            // Swallow rather than throw: a delivery failure (bad SES config, throttling, a
            // typo'd recipient) must never fail the command that triggered it - e.g. a
            // password-reset request already returns a generic success response regardless
            // of whether the account exists, and shouldn't 500 just because SES hiccuped.
            logger.LogError(ex, "Failed to send email to {Email} via SES", toEmail);
        }
    }

    private static (string Html, string Text) BuildBody(string? firstName, string message, string ctaLabel, string ctaLink)
    {
        var greeting = string.IsNullOrWhiteSpace(firstName) ? "Hi," : $"Hi {WebUtility.HtmlEncode(firstName)},";
        var html = $"""
            <p>{greeting}</p>
            <p>{message}</p>
            <p><a href="{ctaLink}" style="display:inline-block;padding:10px 20px;background:#16a34a;color:#fff;border-radius:8px;text-decoration:none;">{ctaLabel}</a></p>
            <p style="color:#666;font-size:13px;">If the button doesn't work, copy and paste this link into your browser:<br />{ctaLink}</p>
            """;
        var text = $"{greeting}\n\n{StripHtml(message)}\n\n{ctaLabel}: {ctaLink}";
        return (html, text);
    }

    private static string StripHtml(string value) => value.Replace("<strong>", "").Replace("</strong>", "");
}
