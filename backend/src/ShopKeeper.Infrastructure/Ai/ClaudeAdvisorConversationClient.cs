namespace ShopKeeper.Infrastructure.Ai;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Thin wrapper over one Anthropic Messages API tool-use turn - builds the request from whatever
/// messages/tools the caller passed in and reports back either final text or the tool(s) Claude
/// wants to run. Never decides what the tools mean or executes one itself; that's
/// AskAdvisorCommandHandler's job (Application layer), which is also where the system prompt's
/// wording is decided - this client stays an unopinionated Anthropic API wrapper.
/// </summary>
public class ClaudeAdvisorConversationClient(HttpClient httpClient, IOptions<AnthropicSettings> options, ILogger<ClaudeAdvisorConversationClient> logger)
    : IAdvisorConversationClient
{
    private readonly AnthropicSettings _settings = options.Value;

    public bool IsConfigured => true; // only ever registered when Anthropic:ApiKey is present

    public async Task<ClaudeTurn> SendAsync(
        string systemPrompt, IReadOnlyList<ClaudeMessage> messages, IReadOnlyList<ClaudeTool> tools, CancellationToken ct = default)
    {
        var payload = new
        {
            model = _settings.Model,
            max_tokens = 500,
            system = systemPrompt,
            tools = tools.Select(t => new { name = t.Name, description = t.Description, input_schema = new { type = "object", properties = new { } } }),
            messages = messages.Select(BuildMessagePayload),
        };

        using var response = await httpClient.PostAsJsonAsync("v1/messages", payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Anthropic API call failed: {StatusCode} {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"Anthropic API returned {(int)response.StatusCode}.");
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        var parsed = JsonSerializer.Deserialize<MessagesResponse>(body)
            ?? throw new InvalidOperationException("Anthropic API returned an empty response body.");

        var toolUses = parsed.Content
            .Where(c => c.Type == "tool_use" && c.Id is not null && c.Name is not null)
            .Select(c => new ClaudeToolUse(c.Id!, c.Name!))
            .ToList();
        if (toolUses.Count > 0)
        {
            return new ClaudeTurn(null, toolUses);
        }

        var text = string.Concat(parsed.Content.Where(c => c.Type == "text").Select(c => c.Text));
        return string.IsNullOrWhiteSpace(text)
            ? throw new InvalidOperationException("Anthropic API response had no text content and no tool use.")
            : new ClaudeTurn(text.Trim(), []);
    }

    private static object BuildMessagePayload(ClaudeMessage message)
    {
        if (message.Text is not null)
        {
            return new { role = message.Role, content = message.Text };
        }
        if (message.ToolUses is not null)
        {
            return new
            {
                role = message.Role,
                content = message.ToolUses.Select(tu => new { type = "tool_use", id = tu.Id, name = tu.Name, input = new { } }),
            };
        }
        if (message.ToolResults is not null)
        {
            return new
            {
                role = message.Role,
                content = message.ToolResults.Select(tr => new { type = "tool_result", tool_use_id = tr.ToolUseId, content = tr.Content }),
            };
        }
        throw new InvalidOperationException("ClaudeMessage must set exactly one of Text/ToolUses/ToolResults.");
    }

    private record MessagesResponse([property: JsonPropertyName("content")] List<ResponseContentBlock> Content);

    private record ResponseContentBlock(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name);
}
