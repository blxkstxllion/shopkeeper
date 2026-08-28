namespace ShopKeeper.Infrastructure.Payments;

using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Enums;

/// <summary>
/// Registered whenever Paystack:SecretKey isn't configured (every dev/CI environment today).
/// Mirrors LoggingEmailSender: IsConfigured is false, and every other method throws - every real
/// caller checks IsConfigured first, so these are a defensive backstop, not a codepath expected
/// to run.
/// </summary>
public class DevPaystackClient : IPaystackClient
{
    public bool IsConfigured => false;

    public Task<PaystackCheckoutSession> InitializeSubscriptionCheckoutAsync(
        string customerEmail, PlanTier tier, string reference, CancellationToken ct = default) =>
        throw new NotSupportedException("Paystack isn't configured in this environment.");

    public Task<PaystackTransactionResult> VerifyTransactionAsync(string reference, CancellationToken ct = default) =>
        throw new NotSupportedException("Paystack isn't configured in this environment.");

    public Task<PaystackSubscriptionInfo?> FindActiveSubscriptionAsync(
        string customerCode, PlanTier tier, CancellationToken ct = default) =>
        throw new NotSupportedException("Paystack isn't configured in this environment.");

    public Task DisableSubscriptionAsync(string subscriptionCode, string emailToken, CancellationToken ct = default) =>
        throw new NotSupportedException("Paystack isn't configured in this environment.");

    public bool VerifyWebhookSignature(string rawBody, string? signatureHeader) =>
        throw new NotSupportedException("Paystack isn't configured in this environment.");
}
