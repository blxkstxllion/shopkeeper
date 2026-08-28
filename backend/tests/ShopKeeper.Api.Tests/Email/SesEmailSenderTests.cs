namespace ShopKeeper.Api.Tests.Email;

using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ShopKeeper.Infrastructure.Identity;
using Xunit;

public class SesEmailSenderTests
{
    private readonly IAmazonSimpleEmailServiceV2 _client = Substitute.For<IAmazonSimpleEmailServiceV2>();
    private readonly SesEmailSender _sender;

    public SesEmailSenderTests()
    {
        var settings = Options.Create(new EmailSettings
        {
            FromAddress = "no-reply@shopkeeper.test",
            FromName = "The Shop Keeper",
            Region = "us-east-1",
            FrontendBaseUrl = "https://app.shopkeeper.test",
        });
        _sender = new SesEmailSender(_client, settings, NullLogger<SesEmailSender>.Instance);
    }

    [Fact]
    public async Task SendPasswordResetAsync_SendsToCorrectRecipient_WithTokenInLink()
    {
        SendEmailRequest? captured = null;
        _client.SendEmailAsync(Arg.Do<SendEmailRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new SendEmailResponse());

        await _sender.SendPasswordResetAsync("owner@business.test", "Amy", "raw-reset-token", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(["owner@business.test"], captured!.Destination.ToAddresses);
        Assert.Equal("The Shop Keeper <no-reply@shopkeeper.test>", captured.FromEmailAddress);
        Assert.Contains("Reset your password", captured.Content.Simple.Subject.Data);
        Assert.Contains(
            "https://app.shopkeeper.test/reset-password?token=raw-reset-token", captured.Content.Simple.Body.Html.Data);
        Assert.Contains(
            "https://app.shopkeeper.test/reset-password?token=raw-reset-token", captured.Content.Simple.Body.Text.Data);
    }

    [Fact]
    public async Task SendBusinessInviteAsync_EscapesHtmlInBusinessAndInviterNames()
    {
        SendEmailRequest? captured = null;
        _client.SendEmailAsync(Arg.Do<SendEmailRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new SendEmailResponse());

        await _sender.SendBusinessInviteAsync(
            "invitee@example.test", "<b>Evil</b> Corp", "<script>alert(1)</script>", "invite-token", CancellationToken.None);

        Assert.NotNull(captured);
        // The subject is plain text (not rendered as HTML by any client), so it intentionally
        // carries the raw names - only the HTML body needs encoding.
        Assert.DoesNotContain("<script>", captured!.Content.Simple.Body.Html.Data);
        Assert.DoesNotContain("<b>Evil</b>", captured.Content.Simple.Body.Html.Data);
        Assert.Contains("&lt;script&gt;", captured.Content.Simple.Body.Html.Data);
    }

    [Fact]
    public async Task SendPasswordResetAsync_DoesNotThrow_WhenSesCallFails()
    {
        _client.SendEmailAsync(Arg.Any<SendEmailRequest>(), Arg.Any<CancellationToken>())
            .Returns<SendEmailResponse>(_ => throw new AmazonSimpleEmailServiceV2Exception("SES is down"));

        // A delivery failure must never bubble up and fail the caller's command (e.g.
        // forgot-password already returns a generic success response either way).
        await _sender.SendPasswordResetAsync("owner@business.test", "Amy", "token", CancellationToken.None);
    }
}
