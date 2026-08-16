namespace ShopKeeper.Application.Advisor.Queries;

using MediatR;
using ShopKeeper.Application.Advisor.Dtos;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;

public record GetAdvisorQuestionsQuery : IRequest<IReadOnlyList<AdvisorQuestionDto>>;

/// <summary>The fixed label set behind the question cards on the Advisor page - backend-driven
/// so the frontend never hardcodes a label that could drift from the id it's paired with.</summary>
public class GetAdvisorQuestionsQueryHandler(ICurrentUserService currentUser)
    : IRequestHandler<GetAdvisorQuestionsQuery, IReadOnlyList<AdvisorQuestionDto>>
{
    public Task<IReadOnlyList<AdvisorQuestionDto>> Handle(GetAdvisorQuestionsQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.AiConsultantUse);

        IReadOnlyList<AdvisorQuestionDto> questions =
        [
            new(AdvisorQuestionId.RevenueThisMonth, "How's my revenue this month?"),
            new(AdvisorQuestionId.ProfitMargin, "What's my profit margin?"),
            new(AdvisorQuestionId.LowStock, "What's running low on stock?"),
            new(AdvisorQuestionId.BestSellingProduct, "What's my best-selling product?"),
            new(AdvisorQuestionId.WorstPerformingProduct, "What's my worst-performing product?"),
            new(AdvisorQuestionId.BranchComparison, "How do my branches compare?"),
            new(AdvisorQuestionId.TopExpenseCategories, "What are my biggest expenses?"),
            new(AdvisorQuestionId.AmIProfitable, "Am I profitable this month?"),
        ];

        return Task.FromResult(questions);
    }
}
