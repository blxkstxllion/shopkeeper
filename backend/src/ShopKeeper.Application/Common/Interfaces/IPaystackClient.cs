namespace ShopKeeper.Application.Common.Interfaces;

using ShopKeeper.Domain.Enums;

public interface IPaystackClient
{
    /// <summary>True only when Paystack:SecretKey is configured. Callers (SetPlanTierCommand,
    /// InitiateCheckoutCommand, PaystackWebhookController) branch on this to preserve today's
    /// fully self-serve, payment-free behavior whenever it's false.</summary>
    bool IsConfigured { get; }

    /// <summary>Builds the checkout callback_url itself from its own configured frontend base URL -
    /// the Application layer never needs to know it, same as IEmailSender's link-building.</summary>
    Task<PaystackCheckoutSession> InitializeSubscriptionCheckoutAsync(
        string customerEmail, PlanTier tier, string reference, CancellationToken ct = default);

    Task<PaystackTransactionResult> VerifyTransactionAsync(string reference, CancellationToken ct = default);

    /// <summary>Looks up the subscription a just-verified transaction created, via the List
    /// Subscriptions endpoint filtered by customer+plan - not by parsing the verify response's
    /// uncertain shape.</summary>
    Task<PaystackSubscriptionInfo?> FindActiveSubscriptionAsync(
        string customerCode, PlanTier tier, CancellationToken ct = default);

    Task DisableSubscriptionAsync(string subscriptionCode, string emailToken, CancellationToken ct = default);

    /// <summary>HMAC-SHA512(secretKey, rawBody) hex, constant-time compared against the
    /// x-paystack-signature header.</summary>
    bool VerifyWebhookSignature(string rawBody, string? signatureHeader);
}

public record PaystackCheckoutSession(string AuthorizationUrl, string AccessCode);

public record PaystackTransactionResult(bool Success, string Status, string CustomerEmail, string CustomerCode);

public record PaystackSubscriptionInfo(
    string SubscriptionCode, string EmailToken, string Status, DateTimeOffset? NextPaymentDate);
