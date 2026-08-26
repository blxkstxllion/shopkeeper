namespace ShopKeeper.Infrastructure.Ai;

public class AnthropicSettings
{
    public const string SectionName = "Anthropic";

    /// <summary>Required for ClaudeAdvisorNarrator to be registered - see DependencyInjection.AddInfrastructure.</summary>
    public string ApiKey { get; set; } = default!;

    public string Model { get; set; } = "claude-haiku-4-5-20251001";
}
