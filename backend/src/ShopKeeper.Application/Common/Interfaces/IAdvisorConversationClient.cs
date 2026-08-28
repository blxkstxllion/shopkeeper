namespace ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// One turn of an Anthropic tool-use conversation. Deliberately narrow: the caller builds the
/// message history and the tool list, this only makes the API call and reports back either final
/// text or the tool(s) Claude wants to run - it never executes a tool itself, so it can only ever
/// select from what the caller offered, never invent a call.
/// </summary>
public interface IAdvisorConversationClient
{
    /// <summary>True only when Anthropic:ApiKey is configured - mirrors IPaystackClient.IsConfigured.
    /// GetAdvisorCapabilitiesQueryHandler exposes this so the frontend can hide the free-text input
    /// entirely rather than let a user submit a question that can only ever hit the fallback.</summary>
    bool IsConfigured { get; }

    Task<ClaudeTurn> SendAsync(
        string systemPrompt, IReadOnlyList<ClaudeMessage> messages, IReadOnlyList<ClaudeTool> tools, CancellationToken ct = default);
}

/// <summary>No input schema - every tool here is a parameterless lookup (see AdvisorQuestions),
/// so Claude only ever selects which one to run, never fills in a value.</summary>
public record ClaudeTool(string Name, string Description);

public record ClaudeToolUse(string Id, string Name);

public record ClaudeToolResult(string ToolUseId, string Content);

public record ClaudeMessage(string Role, string? Text, IReadOnlyList<ClaudeToolUse>? ToolUses, IReadOnlyList<ClaudeToolResult>? ToolResults)
{
    public static ClaudeMessage UserText(string text) => new("user", text, null, null);

    public static ClaudeMessage AssistantToolUse(IReadOnlyList<ClaudeToolUse> toolUses) => new("assistant", null, toolUses, null);

    public static ClaudeMessage UserToolResults(IReadOnlyList<ClaudeToolResult> results) => new("user", null, null, results);
}

/// <summary>Either FinalText is set (Claude answered) or ToolUses is non-empty (Claude wants to
/// run tools first) - never both, matching Anthropic's stop_reason semantics.</summary>
public record ClaudeTurn(string? FinalText, IReadOnlyList<ClaudeToolUse> ToolUses);
