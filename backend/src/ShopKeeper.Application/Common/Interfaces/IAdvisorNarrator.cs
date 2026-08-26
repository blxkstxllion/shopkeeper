namespace ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Rephrases an already-computed, already-correct Advisor answer in natural language - never
/// given raw data and never calls anything itself, so it can only change phrasing, not facts.
/// See GetAdvisorAnswerQueryHandler for the grounded-answer computation this narrates over.
/// </summary>
public interface IAdvisorNarrator
{
    Task<string> NarrateAsync(string questionLabel, string groundedAnswer, CancellationToken ct = default);
}
