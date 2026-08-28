namespace ShopKeeper.Application.Advisor.Queries;

using MediatR;
using ShopKeeper.Application.Advisor.Dtos;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;

public record GetAdvisorAnswerQuery(AdvisorQuestionId QuestionId, Guid? BranchId)
    : IRequest<AdvisorAnswerDto>, IRequirePlanFeature
{
    public bool RequiresReports => false;
    public bool RequiresAi => true;
    public bool RequiresCustomRoles => false;
}

/// <summary>
/// Answers one of the 8 fixed Advisor questions by delegating the actual computation to
/// AdvisorCalculations (shared with the free-text path, AskAdvisorCommand), then narrating the
/// grounded, already-correct result in natural language - an LLM (via IAdvisorNarrator) only
/// rephrases that string, it never computes or invents a number itself.
/// </summary>
public class GetAdvisorAnswerQueryHandler(
    AdvisorCalculations calculations, ICurrentUserService currentUser, IAdvisorNarrator narrator)
    : IRequestHandler<GetAdvisorAnswerQuery, AdvisorAnswerDto>
{
    public async Task<AdvisorAnswerDto> Handle(GetAdvisorAnswerQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.AiConsultantUse);

        var answer = await calculations.ComputeAsync(request.QuestionId, request.BranchId, cancellationToken);

        try
        {
            answer = await narrator.NarrateAsync(request.QuestionId.ToString(), answer, cancellationToken);
        }
        catch (Exception)
        {
            // Best-effort enhancement only - the grounded answer above is already correct and
            // complete, so a Claude outage/error degrades phrasing, never the feature itself.
            // ClaudeAdvisorNarrator already logs the underlying failure before rethrowing.
        }

        return new AdvisorAnswerDto(answer, DateTimeOffset.UtcNow);
    }
}
