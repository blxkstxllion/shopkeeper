namespace ShopKeeper.Infrastructure.Ai;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Writes the exported report's executive summary via Claude. Same safety shape as
/// ClaudeAdvisorNarrator: given only a small, already-verified set of facts (never raw report
/// rows), constrained by its system prompt to state only those numbers - a bad or hallucinated
/// response can only be bad *phrasing*, not a wrong figure, since every number it's allowed to
/// use is already printed elsewhere in the same document. Any failure is logged and rethrown;
/// GenerateBusinessReportCommandHandler decides whether to fall back.
/// </summary>
public class ClaudeReportSummarizer(HttpClient httpClient, IOptions<AnthropicSettings> options, ILogger<ClaudeReportSummarizer> logger)
    : IReportSummarizer
{
    private const string SystemPrompt =
        "You are writing the executive summary at the top of a business report. Use ONLY the " +
        "facts given below - never invent, adjust, omit, or round a number, percentage, or name " +
        "differently than given. Write 3-5 sentences, professional and concise, suitable to " +
        "print at the top of a printed report. Do not use headings or bullet points.";

    private readonly AnthropicSettings _settings = options.Value;

    public async Task<string> SummarizeAsync(ReportFacts facts, CancellationToken ct = default)
    {
        var factsText = JsonSerializer.Serialize(facts);

        using var response = await httpClient.PostAsJsonAsync(
            "v1/messages",
            new
            {
                model = _settings.Model,
                max_tokens = 400,
                system = SystemPrompt,
                messages = new[]
                {
                    new { role = "user", content = $"Facts (JSON): {factsText}" },
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
