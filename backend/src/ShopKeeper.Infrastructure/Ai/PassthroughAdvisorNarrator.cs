namespace ShopKeeper.Infrastructure.Ai;

using ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Returns the grounded answer unchanged. Used both as the production fallback when
/// Anthropic:ApiKey isn't configured (same "absence never breaks startup" pattern as
/// LoggingEmailSender/DevPaystackClient), and directly in AdvisorTests.cs - pure passthrough is
/// exactly what those tests already assert against (exact substrings like "GHS 50.00").
/// </summary>
public class PassthroughAdvisorNarrator : IAdvisorNarrator
{
    public Task<string> NarrateAsync(string questionLabel, string groundedAnswer, CancellationToken ct = default) =>
        Task.FromResult(groundedAnswer);
}
