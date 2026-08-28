namespace ShopKeeper.Application.Advisor.Queries;

using MediatR;
using ShopKeeper.Application.Advisor;
using ShopKeeper.Application.Advisor.Dtos;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;

public record GetAdvisorQuestionsQuery : IRequest<IReadOnlyList<AdvisorQuestionDto>>, IRequirePlanFeature
{
    public bool RequiresReports => false;
    public bool RequiresAi => true;
    public bool RequiresCustomRoles => false;
}

/// <summary>The fixed label set behind the question cards on the Advisor page - backend-driven
/// so the frontend never hardcodes a label that could drift from the id it's paired with.</summary>
public class GetAdvisorQuestionsQueryHandler(ICurrentUserService currentUser)
    : IRequestHandler<GetAdvisorQuestionsQuery, IReadOnlyList<AdvisorQuestionDto>>
{
    public Task<IReadOnlyList<AdvisorQuestionDto>> Handle(GetAdvisorQuestionsQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.AiConsultantUse);

        return Task.FromResult(AdvisorQuestions.All);
    }
}
