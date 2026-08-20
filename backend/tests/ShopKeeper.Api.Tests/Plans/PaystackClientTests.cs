namespace ShopKeeper.Api.Tests.Plans;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShopKeeper.Infrastructure.Payments;

public class PaystackClientTests
{
    private static PaystackClient BuildClient(string signingValue) => new(
        new HttpClient { BaseAddress = new Uri("https://api.paystack.co/") },
        Options.Create(new PaystackSettings { SecretKey = signingValue }),
        NullLogger<PaystackClient>.Instance);

    [Fact]
    public void VerifyWebhookSignature_RoundTrips()
    {
        const string testSigningValue = "sk_test_roundtrip_secret";
        const string rawBody = """{"event":"charge.success","data":{"reference":"chk_test"}}""";
        var client = BuildClient(testSigningValue);

        var expectedSignature = Convert.ToHexString(
            System.Security.Cryptography.HMACSHA512.HashData(
                System.Text.Encoding.UTF8.GetBytes(testSigningValue), System.Text.Encoding.UTF8.GetBytes(rawBody)))
            .ToLowerInvariant();

        Assert.True(client.VerifyWebhookSignature(rawBody, expectedSignature));
    }

    [Fact]
    public void VerifyWebhookSignature_TamperedBody_ReturnsFalse()
    {
        const string testSigningValue = "sk_test_roundtrip_secret";
        const string rawBody = """{"event":"charge.success","data":{"reference":"chk_test"}}""";
        var client = BuildClient(testSigningValue);

        var signatureForOriginalBody = Convert.ToHexString(
            System.Security.Cryptography.HMACSHA512.HashData(
                System.Text.Encoding.UTF8.GetBytes(testSigningValue), System.Text.Encoding.UTF8.GetBytes(rawBody)))
            .ToLowerInvariant();

        var tamperedBody = rawBody.Replace("charge.success", "subscription.disable");

        Assert.False(client.VerifyWebhookSignature(tamperedBody, signatureForOriginalBody));
    }

    [Fact]
    public void VerifyWebhookSignature_MissingHeader_ReturnsFalse()
    {
        var client = BuildClient("sk_test_roundtrip_secret");
        Assert.False(client.VerifyWebhookSignature("{}", null));
    }
}
