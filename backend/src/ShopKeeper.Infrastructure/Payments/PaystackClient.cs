namespace ShopKeeper.Infrastructure.Payments;

using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Enums;

/// <summary>
/// Real Paystack API client, first typed-HttpClient usage in this codebase (see
/// DependencyInjection.AddInfrastructure - base address and Authorization header are set once
/// there). Response DTOs use explicit JsonPropertyName rather than a snake_case naming policy,
/// since .NET 8's System.Text.Json has no built-in snake_case policy (that landed in .NET 9).
/// </summary>
public class PaystackClient(HttpClient httpClient, IOptions<PaystackSettings> options, ILogger<PaystackClient> logger)
    : IPaystackClient
{
    private readonly PaystackSettings _settings = options.Value;

    public bool IsConfigured => true; // only ever registered when Paystack:SecretKey is present

    public async Task<PaystackCheckoutSession> InitializeSubscriptionCheckoutAsync(
        string customerEmail, PlanTier tier, string reference, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "transaction/initialize",
            new
            {
                email = customerEmail,
                plan = PlanCodeFor(tier),
                reference,
                callback_url = $"{_settings.FrontendBaseUrl}/app/billing/callback",
            },
            ct);

        var body = await ReadResponseAsync<InitializeTransactionResponse>(response, ct);
        return new PaystackCheckoutSession(body.Data.AuthorizationUrl, body.Data.AccessCode);
    }

    public async Task<PaystackTransactionResult> VerifyTransactionAsync(string reference, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"transaction/verify/{Uri.EscapeDataString(reference)}", ct);
        var body = await ReadResponseAsync<VerifyTransactionResponse>(response, ct);
        var success = string.Equals(body.Data.Status, "success", StringComparison.OrdinalIgnoreCase);
        return new PaystackTransactionResult(success, body.Data.Status, body.Data.Customer.Email, body.Data.Customer.CustomerCode);
    }

    public async Task<PaystackSubscriptionInfo?> FindActiveSubscriptionAsync(
        string customerCode, PlanTier tier, CancellationToken ct = default)
    {
        var planCode = PlanCodeFor(tier);
        using var response = await httpClient.GetAsync(
            $"subscription?customer={Uri.EscapeDataString(customerCode)}&plan={Uri.EscapeDataString(planCode)}", ct);
        var body = await ReadResponseAsync<ListSubscriptionsResponse>(response, ct);
        var subscription = body.Data.FirstOrDefault();
        return subscription is null
            ? null
            : new PaystackSubscriptionInfo(
                subscription.SubscriptionCode, subscription.EmailToken, subscription.Status, subscription.NextPaymentDate);
    }

    public async Task DisableSubscriptionAsync(string subscriptionCode, string emailToken, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "subscription/disable", new { code = subscriptionCode, token = emailToken }, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public bool VerifyWebhookSignature(string rawBody, string? signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        var computedHash = HMACSHA512.HashData(Encoding.UTF8.GetBytes(_settings.SecretKey), Encoding.UTF8.GetBytes(rawBody));
        var computedHex = Convert.ToHexString(computedHash).ToLowerInvariant();

        var computedBytes = Encoding.UTF8.GetBytes(computedHex);
        var providedBytes = Encoding.UTF8.GetBytes(signatureHeader);
        return computedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(computedBytes, providedBytes);
    }

    private string PlanCodeFor(PlanTier tier) => tier switch
    {
        PlanTier.Business => _settings.BusinessPlanCode,
        PlanTier.BusinessAi => _settings.BusinessAiPlanCode,
        PlanTier.Enterprise => _settings.EnterprisePlanCode,
        PlanTier.EnterpriseAi => _settings.EnterpriseAiPlanCode,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Plan tier has no Paystack plan code."),
    };

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        logger.LogError("Paystack API call failed: {StatusCode} {Body}", response.StatusCode, body);
        throw new PaystackApiException($"Paystack API returned {(int)response.StatusCode}.");
    }

    private async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureSuccessAsync(response, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(body)
            ?? throw new PaystackApiException("Paystack API returned an empty response body.");
    }

    private record InitializeTransactionResponse([property: JsonPropertyName("data")] InitializeTransactionData Data);

    private record InitializeTransactionData(
        [property: JsonPropertyName("authorization_url")] string AuthorizationUrl,
        [property: JsonPropertyName("access_code")] string AccessCode);

    private record VerifyTransactionResponse([property: JsonPropertyName("data")] VerifyTransactionData Data);

    private record VerifyTransactionData(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("customer")] VerifyTransactionCustomer Customer);

    private record VerifyTransactionCustomer(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("customer_code")] string CustomerCode);

    private record ListSubscriptionsResponse([property: JsonPropertyName("data")] List<SubscriptionListItem> Data);

    private record SubscriptionListItem(
        [property: JsonPropertyName("subscription_code")] string SubscriptionCode,
        [property: JsonPropertyName("email_token")] string EmailToken,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("next_payment_date")] DateTimeOffset? NextPaymentDate);
}
