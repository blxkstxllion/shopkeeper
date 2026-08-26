namespace ShopKeeper.Infrastructure.Ai;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Real Claude (Anthropic Messages API) client, first Anthropic call in this codebase. Base
/// address and auth headers are set once in DependencyInjection.AddInfrastructure, same as
/// PaystackClient's typed-HttpClient setup.
///
/// Deliberately narrow: this only rephrases a string the caller already computed and knows is
/// correct (see IAdvisorNarrator) - it is never given raw business data and never decides what
/// to compute, so a bad or hallucinated response can only be bad *phrasing*, not a wrong number.
/// Any failure is logged and rethrown; GetAdvisorAnswerQueryHandler is the one that decides to
/// fall back to the grounded answer, not this client.
/// </summary>
public class ClaudeAdvisorNarrator(HttpClient httpClient, IOptions<AnthropicSettings> options, ILogger<ClaudeAdvisorNarrator> logger)
    : IAdvisorNarrator
{
    private const string SystemPrompt =
        "You are a warm, concise business advisor. Rephrase the factual answer below in natural " +
        "language. Rules: never change, add, or remove any number, percentage, product name, or " +
        "currency figure - repeat them exactly as given. You may add one brief, generic " +
        "suggestion if natural. Keep it to 2-3 sentences. Do not repeat the question.";

    private readonly AnthropicSettings _settings = options.Value;

    public async Task<string> NarrateAsync(string questionLabel, string groundedAnswer, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "v1/messages",
            new
            {
                model = _settings.Model,
                max_tokens = 300,
                system = SystemPrompt,
                messages = new[]
                {
                    new { role = "user", content = $"Question: {questionLabel}\nFactual answer: {groundedAnswer}" },
                },
            },
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Anthropic API call failed: {StatusCode} {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"Anthropic API returned {(int)response.StatusCode}.");
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        var parsed = JsonSerializer.Deserialize<MessagesResponse>(body)
            ?? throw new InvalidOperationException("Anthropic API returned an empty response body.");

        var text = parsed.Content.FirstOrDefault(c => c.Type == "text")?.Text;
        return string.IsNullOrWhiteSpace(text)
            ? throw new InvalidOperationException("Anthropic API response had no text content.")
            : text.Trim();
    }

    private record MessagesResponse([property: JsonPropertyName("content")] List<MessagesContentBlock> Content);

    private record MessagesContentBlock(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text);
}
