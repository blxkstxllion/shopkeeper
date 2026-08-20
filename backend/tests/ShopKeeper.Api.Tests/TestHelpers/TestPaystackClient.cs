namespace ShopKeeper.Api.Tests.TestHelpers;

using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Enums;

/// <summary>Configurable capturing fake for IPaystackClient. Unlike TestEmailSender (pure
/// fire-and-forget capture), IPaystackClient's methods return values handler logic branches on,
/// so this fake also lets tests control what it "returns" via the Next* properties.</summary>
public class TestPaystackClient : IPaystackClient
{
    public bool IsConfigured { get; set; } = true;

    public PaystackCheckoutSession NextCheckoutSession { get; set; } =
        new("https://checkout.paystack.com/test", "access_code_test");

    public PaystackTransactionResult NextVerifyResult { get; set; } =
        new(true, "success", "owner@example.com", "CUS_test");

    public PaystackSubscriptionInfo? NextSubscriptionInfo { get; set; } =
        new("SUB_test", "tok_test", "active", DateTimeOffset.UtcNow.AddDays(30));

    public bool NextSignatureValid { get; set; } = true;

    public (string CustomerEmail, PlanTier Tier, string Reference)? LastCheckoutRequest { get; private set; }

    public string? LastVerifiedReference { get; private set; }

    public (string SubscriptionCode, string EmailToken)? LastDisabledSubscription { get; private set; }

    public Task<PaystackCheckoutSession> InitializeSubscriptionCheckoutAsync(
        string customerEmail, PlanTier tier, string reference, CancellationToken ct = default)
    {
        LastCheckoutRequest = (customerEmail, tier, reference);
        return Task.FromResult(NextCheckoutSession);
    }

    public Task<PaystackTransactionResult> VerifyTransactionAsync(string reference, CancellationToken ct = default)
    {
        LastVerifiedReference = reference;
        return Task.FromResult(NextVerifyResult);
    }

    public Task<PaystackSubscriptionInfo?> FindActiveSubscriptionAsync(
        string customerCode, PlanTier tier, CancellationToken ct = default) =>
        Task.FromResult(NextSubscriptionInfo);

    public Task DisableSubscriptionAsync(string subscriptionCode, string emailToken, CancellationToken ct = default)
    {
        LastDisabledSubscription = (subscriptionCode, emailToken);
        return Task.CompletedTask;
    }

    public bool VerifyWebhookSignature(string rawBody, string? signatureHeader) => NextSignatureValid;
}
