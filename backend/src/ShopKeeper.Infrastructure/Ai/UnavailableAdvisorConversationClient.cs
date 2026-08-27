namespace ShopKeeper.Infrastructure.Ai;

using ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Registered when Anthropic:ApiKey isn't configured. Unlike PassthroughAdvisorNarrator/
/// PassthroughReportSummarizer, there's no safe non-AI way to interpret free text, so this
/// deliberately throws rather than pretending to answer - AskAdvisorCommandHandler catches it and
/// falls back to a fixed message pointing at the 8 quick questions. The frontend also hides the
/// free-text input entirely when Anthropic isn't configured (see GetAdvisorCapabilitiesQuery), so
/// in practice this only fires if the key is removed between page load and submit.
/// </summary>
public class UnavailableAdvisorConversationClient : IAdvisorConversationClient
{
    public bool IsConfigured => false;

    public Task<ClaudeTurn> SendAsync(
        string systemPrompt, IReadOnlyList<ClaudeMessage> messages, IReadOnlyList<ClaudeTool> tools, CancellationToken ct = default) =>
        throw new InvalidOperationException("Free-text Advisor questions require Anthropic:ApiKey to be configured.");
}
