namespace ShopKeeper.Application.Plans.Commands;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Plans;
using ShopKeeper.Domain.Entities;

public record ProcessPaystackWebhookCommand(string RawBody) : IRequest;

/// <summary>
/// Applies a Paystack webhook delivery. NOT on the critical path for a purchase to succeed -
/// VerifyCheckoutCommand (synchronous, on browser redirect) is. This exists for ongoing resync:
/// refreshing subscription status on renewal charges, and reacting to a Paystack-side
/// cancellation after failed payment retries - a role it fulfills even if it never fires locally.
/// No ICurrentUserService: a webhook caller has no JWT, so the target business is resolved
/// directly from data in the payload instead. Parsing is deliberately defensive (every access
/// goes through TryGetString/TryGetObject below, which never throw regardless of shape) since the
/// exact real-world JSON layout of each event hasn't been independently verified against a live
/// payload yet - see the deployment plan's "what cannot be verified without a live host" section.
/// </summary>
public class ProcessPaystackWebhookCommandHandler(IAppDbContext db, ILogger<ProcessPaystackWebhookCommandHandler> logger)
    : IRequestHandler<ProcessPaystackWebhookCommand>
{
    public async Task Handle(ProcessPaystackWebhookCommand request, CancellationToken cancellationToken)
    {
        var hash = Sha256Hex(request.RawBody);
        if (await db.PaystackWebhookEvents.AnyAsync(e => e.RawPayloadHash == hash, cancellationToken))
        {
            return; // already processed - Paystack resends identical bytes on retry
        }

        using var document = JsonDocument.Parse(request.RawBody);
        var root = document.RootElement;
        var eventType = TryGetString(root, "event");
        var data = TryGetObject(root, "data");

        if (data.ValueKind != JsonValueKind.Object)
        {
            logger.LogWarning("Paystack webhook {EventType}: \"data\" is missing or not an object.", eventType);
        }
        else
        {
            var business = await ResolveBusinessAsync(data, cancellationToken);
            if (business is null)
            {
                logger.LogWarning("Paystack webhook {EventType}: could not resolve a target business.", eventType);
            }
            else
            {
                ApplyEvent(business, eventType, data);
            }
        }

        db.PaystackWebhookEvents.Add(new PaystackWebhookEvent { RawPayloadHash = hash, EventType = eventType ?? "unknown" });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Business?> ResolveBusinessAsync(JsonElement data, CancellationToken cancellationToken)
    {
        var customerCode = TryGetString(TryGetObject(data, "customer"), "customer_code");
        if (customerCode is not null)
        {
            var byCustomer = await db.Businesses.FirstOrDefaultAsync(b => b.PaystackCustomerCode == customerCode, cancellationToken);
            if (byCustomer is not null)
            {
                return byCustomer;
            }
        }

        // Fallback: closes the race where this webhook arrives before the browser redirects back
        // and VerifyCheckoutCommand has had a chance to populate PaystackCustomerCode.
        if (CheckoutReference.TryParse(TryGetString(data, "reference"), out var businessId, out _))
        {
            return await db.Businesses.FirstOrDefaultAsync(b => b.Id == businessId, cancellationToken);
        }

        return null;
    }

    private static void ApplyEvent(Business business, string? eventType, JsonElement data)
    {
        switch (eventType)
        {
            case "subscription.create":
                business.PaystackSubscriptionCode = TryGetString(data, "subscription_code") ?? business.PaystackSubscriptionCode;
                business.PaystackSubscriptionEmailToken = TryGetString(data, "email_token") ?? business.PaystackSubscriptionEmailToken;
                business.PaystackSubscriptionStatus = TryGetString(data, "status") ?? business.PaystackSubscriptionStatus;
                business.PaystackSubscriptionPlanCode =
                    TryGetString(TryGetObject(data, "plan"), "plan_code") ?? business.PaystackSubscriptionPlanCode;

                if (data.TryGetProperty("next_payment_date", out var nextPaymentProp)
                    && nextPaymentProp.ValueKind == JsonValueKind.String
                    && nextPaymentProp.TryGetDateTimeOffset(out var nextPayment))
                {
                    business.PaystackCurrentPeriodEnd = nextPayment;
                }

                break;

            case "subscription.disable":
                business.PaystackSubscriptionStatus = TryGetString(data, "status") ?? business.PaystackSubscriptionStatus;
                break;

            case "charge.success":
                // Only a subscription-driven charge (this app never uses Paystack for one-off
                // payments) - a plan-less charge.success isn't ours to act on. A successful
                // recurring charge implies the subscription is healthy again (clears a prior
                // "attention"/failed-retry status) - deliberately not touching period-end here,
                // since this event's exact next-payment-date field wasn't independently
                // confirmed (see the deployment plan's confidence notes); subscription.create
                // and manual verification remain the reliable source for that.
                if (TryGetObject(data, "plan").ValueKind == JsonValueKind.Object)
                {
                    business.PaystackSubscriptionStatus = "active";
                }

                break;
        }
    }

    private static string? TryGetString(JsonElement parent, string propertyName) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static JsonElement TryGetObject(JsonElement parent, string propertyName) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.Object
            ? value
            : default;

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
