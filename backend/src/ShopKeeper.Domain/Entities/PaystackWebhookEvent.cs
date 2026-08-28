namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

/// <summary>
/// One row per processed Paystack webhook delivery, keyed on a hash of the raw payload
/// rather than a Paystack event ID - Paystack resends identical bytes on retry, and this
/// avoids depending on a stable unique-ID field existing across every event type. Not tied
/// to a Business: the target business is resolved dynamically while processing, and a
/// delivery might arrive before it can be determined at all (see ProcessPaystackWebhookCommand).
/// </summary>
public class PaystackWebhookEvent : BaseEntity
{
    public string RawPayloadHash { get; set; } = default!;
    public string EventType { get; set; } = default!;
}
