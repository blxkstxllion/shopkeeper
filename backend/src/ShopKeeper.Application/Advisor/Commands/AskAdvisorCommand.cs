namespace ShopKeeper.Application.Advisor.Commands;

using FluentValidation;
using MediatR;
using ShopKeeper.Application.Advisor.Dtos;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;

public record AskAdvisorCommand(string Question, Guid? BranchId) : IRequest<AdvisorAnswerDto>, IRequirePlanFeature
{
    public bool RequiresReports => false;
    public bool RequiresAi => true;
    public bool RequiresCustomRoles => false;
}

public class AskAdvisorCommandValidator : AbstractValidator<AskAdvisorCommand>
{
    public AskAdvisorCommandValidator()
    {
        RuleFor(x => x.Question).NotEmpty().MaximumLength(300);
    }
}

/// <summary>
/// Answers a free-text question by having Claude pick which of the 8 AdvisorQuestions tools to
/// run (real Anthropic tool-calling, not free-form computation) - the same closed, tested set
/// GetAdvisorAnswerQueryHandler routes fixed-button clicks to. Claude never computes a number
/// itself: it only selects a tool, AdvisorCalculations runs the real query, and Claude's final
/// reply is instructed to state only what the tool results said. This preserves the same
/// "can't disagree with what the app shows" guarantee as the fixed-question path, just reached via
/// natural language instead of a button click.
///
/// Capped at one round of tool execution - if Claude asks for tools again after seeing results,
/// or anything else goes wrong (unrecognized tool, HTTP failure, Anthropic not configured), this
/// falls back to a fixed message rather than guessing. There's no template fallback here the way
/// there is for the fixed questions/report summaries, because there's no safe non-AI way to
/// interpret free text - the frontend also hides this input whenever Anthropic isn't configured
/// (see GetAdvisorCapabilitiesQuery).
/// </summary>
public class AskAdvisorCommandHandler(
    IAdvisorConversationClient claude, AdvisorCalculations calculations, ICurrentUserService currentUser)
    : IRequestHandler<AskAdvisorCommand, AdvisorAnswerDto>
{
    private const string SystemPrompt =
        "You are a business advisor tool-router. You may ONLY answer using the tools provided - " +
        "each one returns a real, already-verified fact about the user's business. Call one or " +
        "more relevant tools, then summarize their results in 2-4 sentences. Never state a number " +
        "that didn't come from a tool result, and never use outside knowledge. If no tool matches " +
        "the question, say so honestly and suggest one of the available topics instead of guessing.";

    private const string FallbackAnswer =
        "I can only answer questions about revenue, profit margin, stock levels, top/worst " +
        "products, branch comparison, expenses, and profitability right now - try one of the " +
        "quick questions above, or rephrase.";

    public async Task<AdvisorAnswerDto> Handle(AskAdvisorCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.AiConsultantUse);

        var tools = AdvisorQuestions.All.Select(q => new ClaudeTool(q.Id.ToString(), q.Label)).ToList();
        var answer = await TryAnswerAsync(request, tools, cancellationToken);

        return new AdvisorAnswerDto(answer ?? FallbackAnswer, DateTimeOffset.UtcNow);
    }

    private async Task<string?> TryAnswerAsync(AskAdvisorCommand request, IReadOnlyList<ClaudeTool> tools, CancellationToken ct)
    {
        try
        {
            var messages = new List<ClaudeMessage> { ClaudeMessage.UserText(request.Question) };

            var firstTurn = await claude.SendAsync(SystemPrompt, messages, tools, ct);
            if (firstTurn.FinalText is not null)
            {
                return firstTurn.FinalText;
            }

            var toolResults = new List<ClaudeToolResult>();
            foreach (var toolUse in firstTurn.ToolUses)
            {
                var resultText = Enum.TryParse<AdvisorQuestionId>(toolUse.Name, out var questionId)
                    ? await calculations.ComputeAsync(questionId, request.BranchId, ct)
                    : "This topic isn't available.";
                toolResults.Add(new ClaudeToolResult(toolUse.Id, resultText));
            }

            messages.Add(ClaudeMessage.AssistantToolUse(firstTurn.ToolUses));
            messages.Add(ClaudeMessage.UserToolResults(toolResults));

            var secondTurn = await claude.SendAsync(SystemPrompt, messages, tools, ct);
            return secondTurn.FinalText; // null (not a fallback string) if Claude asked for tools again - caller falls back.
        }
        catch (Exception)
        {
            // Covers UnavailableAdvisorConversationClient (Anthropic not configured), HTTP
            // failures, and malformed responses alike - all resolve to the same honest fallback.
            return null;
        }
    }
}
