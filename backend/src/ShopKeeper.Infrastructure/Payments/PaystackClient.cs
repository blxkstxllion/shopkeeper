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
        var planCode = PlanCodeFor(tier);

        // Contrary to what secondary-source research assumed pre-launch (Paystack's own docs
        // site 403'd every fetch attempt during planning), passing `plan` alone is NOT enough -
        // live testing confirmed Initialize Transaction rejects the request with "Invalid Amount
        // Sent" unless `amount` is also present. Fetching the plan's own amount here (rather than
        // duplicating pricing into this app's config) keeps the checkout amount authoritative
        // against whatever the plan is actually configured for in the Paystack dashboard.
        var amount = await FetchPlanAmountAsync(planCode, ct);

        using var response = await httpClient.PostAsJsonAsync(
            "transaction/initialize",
            new
            {
                email = customerEmail,
                plan = planCode,
                amount,
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

        // The `customer`/`plan` query filters on this endpoint take Paystack's internal numeric
        // IDs, not the CUS_xxx/PLN_xxx codes this app tracks everywhere else (confirmed live -
        // passing the codes silently returns an empty list rather than erroring) - since this app
        // never captures those numeric IDs, filter client-side against the unfiltered list instead.
        //
        // Parsed via JsonDocument rather than a typed record: a 6-parameter record with two
        // nested record-typed parameters (Customer, Plan) hit a genuine System.Text.Json
        // limitation here ("Deserialization of types without a parameterless constructor, a
        // singular parameterized constructor..." - reproduced consistently, not a hot-reload
        // artifact) that a flatter/fewer-parameter shape doesn't run into elsewhere in this file.
        using var response = await httpClient.GetAsync("subscription", ct);
        await EnsureSuccessAsync(response, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var document = JsonDocument.Parse(body);

        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in data.EnumerateArray())
        {
            var itemCustomerCode = item.TryGetProperty("customer", out var customer)
                ? customer.GetProperty("customer_code").GetString()
                : null;
            var itemPlanCode = item.TryGetProperty("plan", out var plan) ? plan.GetProperty("plan_code").GetString() : null;

            if (itemCustomerCode != customerCode || itemPlanCode != planCode)
            {
                continue;
            }

            var nextPaymentDate = item.TryGetProperty("next_payment_date", out var nextPaymentProp)
                && nextPaymentProp.ValueKind == JsonValueKind.String
                && nextPaymentProp.TryGetDateTimeOffset(out var parsed)
                ? parsed
                : (DateTimeOffset?)null;

            return new PaystackSubscriptionInfo(
                item.GetProperty("subscription_code").GetString()!,
                item.GetProperty("email_token").GetString()!,
                item.GetProperty("status").GetString()!,
                nextPaymentDate);
        }

        return null;
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

    private async Task<long> FetchPlanAmountAsync(string planCode, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync($"plan/{Uri.EscapeDataString(planCode)}", ct);
        var body = await ReadResponseAsync<FetchPlanResponse>(response, ct);
        return body.Data.Amount;
    }

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

    private record FetchPlanResponse([property: JsonPropertyName("data")] FetchPlanData Data);

    private record FetchPlanData([property: JsonPropertyName("amount")] long Amount);

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

}
