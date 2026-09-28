namespace ShopKeeper.Api.Tests.Email;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShopKeeper.Infrastructure.Identity;
using Xunit;

public class ResendEmailSenderTests
{
    /// <summary>Captures the outgoing request instead of hitting the real API - Resend has no
    /// interface to substitute the way SES's SDK client does (see SesEmailSenderTests), so this
    /// intercepts at the HttpMessageHandler level, one layer below where AddHttpClient's typed
    /// client (see DependencyInjection) would normally send it.</summary>
    private sealed class FakeHandler(HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public JsonElement? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                var raw = await request.Content.ReadAsStringAsync(cancellationToken);
                LastBody = JsonDocument.Parse(raw).RootElement.Clone();
            }
            return new HttpResponseMessage(statusCode) { Content = JsonContent.Create(new { id = "test-id" }) };
        }
    }

    private static (ResendEmailSender Sender, FakeHandler Handler) Build(HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new FakeHandler(statusCode);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var settings = Options.Create(new EmailSettings
        {
            FromAddress = "no-reply@shopkeeper.test",
            FromName = "The Shop Keeper",
            FrontendBaseUrl = "https://app.shopkeeper.test",
        });
        return (new ResendEmailSender(client, settings, NullLogger<ResendEmailSender>.Instance), handler);
    }

    [Fact]
    public async Task SendPasswordResetAsync_SendsToCorrectRecipient_WithTokenInLink()
    {
        var (sender, handler) = Build();

        await sender.SendPasswordResetAsync("owner@business.test", "Amy", "raw-reset-token", CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://api.resend.com/emails", handler.LastRequest.RequestUri!.ToString());
        // Authorization is set centrally by AddInfrastructure's typed-client config (matching
        // PaystackClient), not by this class - not this test's concern.

        var body = handler.LastBody!.Value;
        Assert.Equal("owner@business.test", body.GetProperty("to")[0].GetString());
        Assert.Equal("The Shop Keeper <no-reply@shopkeeper.test>", body.GetProperty("from").GetString());
        Assert.Contains("Reset your password", body.GetProperty("subject").GetString());
        Assert.Contains(
            "https://app.shopkeeper.test/reset-password?token=raw-reset-token", body.GetProperty("html").GetString());
        Assert.Contains(
            "https://app.shopkeeper.test/reset-password?token=raw-reset-token", body.GetProperty("text").GetString());
    }

    [Fact]
    public async Task SendBusinessInviteAsync_EscapesHtmlInBusinessAndInviterNames()
    {
        var (sender, handler) = Build();

        await sender.SendBusinessInviteAsync(
            "invitee@example.test", "<b>Evil</b> Corp", "<script>alert(1)</script>", "invite-token", CancellationToken.None);

        var html = handler.LastBody!.Value.GetProperty("html").GetString()!;
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<b>Evil</b>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task SendReportEmailAsync_IncludesAttachmentAsBase64()
    {
        var (sender, handler) = Build();
        var fileBytes = "not a real pdf"u8.ToArray();

        await sender.SendReportEmailAsync(
            "owner@business.test", "Ama's Shop", fileBytes, "report.pdf", "application/pdf", CancellationToken.None);

        var attachments = handler.LastBody!.Value.GetProperty("attachments");
        Assert.Equal(1, attachments.GetArrayLength());
        var attachment = attachments[0];
        Assert.Equal("report.pdf", attachment.GetProperty("filename").GetString());
        Assert.Equal("application/pdf", attachment.GetProperty("content_type").GetString());
        Assert.Equal(Convert.ToBase64String(fileBytes), attachment.GetProperty("content").GetString());
    }

    [Fact]
    public async Task SendPasswordResetAsync_DoesNotThrow_WhenResendReturnsAnError()
    {
        var (sender, _) = Build(HttpStatusCode.Unauthorized);

        // A delivery failure must never bubble up and fail the caller's command (e.g.
        // forgot-password already returns a generic success response either way).
        await sender.SendPasswordResetAsync("owner@business.test", "Amy", "token", CancellationToken.None);
    }

    [Fact]
    public async Task SendPasswordResetAsync_DoesNotThrow_WhenHttpClientThrows()
    {
        var handler = new ThrowingHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var settings = Options.Create(new EmailSettings
        {
            FromAddress = "no-reply@shopkeeper.test",
            FromName = "The Shop Keeper",
            FrontendBaseUrl = "https://app.shopkeeper.test",
        });
        var sender = new ResendEmailSender(client, settings, NullLogger<ResendEmailSender>.Instance);

        await sender.SendPasswordResetAsync("owner@business.test", "Amy", "token", CancellationToken.None);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Resend is down");
    }
}
