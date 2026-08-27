namespace ShopKeeper.Api.Tests.TestHelpers;

using ShopKeeper.Application.Common.Interfaces;

/// <summary>Configurable capturing fake for IAdvisorConversationClient - queues one ClaudeTurn per
/// expected call (tool-use conversations are inherently sequence-dependent: the first-turn and
/// second-turn responses differ), and records every call's messages so a test can assert on
/// exactly what was sent to Claude (e.g. that a tool_result carried the real grounded number).</summary>
public class TestAdvisorConversationClient : IAdvisorConversationClient
{
    public bool IsConfigured { get; set; } = true;

    public Queue<ClaudeTurn> Responses { get; } = new();

    public Exception? ThrowException { get; set; }

    public int CallCount { get; private set; }

    public List<IReadOnlyList<ClaudeMessage>> CallHistory { get; } = [];

    public Task<ClaudeTurn> SendAsync(
        string systemPrompt, IReadOnlyList<ClaudeMessage> messages, IReadOnlyList<ClaudeTool> tools, CancellationToken ct = default)
    {
        CallCount++;
        CallHistory.Add(messages);

        if (ThrowException is not null)
        {
            throw ThrowException;
        }

        return Responses.Count > 0
            ? Task.FromResult(Responses.Dequeue())
            : throw new InvalidOperationException("TestAdvisorConversationClient has no more queued responses.");
    }
}
