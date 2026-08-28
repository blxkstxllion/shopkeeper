namespace ShopKeeper.Application.Advisor;

using ShopKeeper.Application.Advisor.Dtos;

/// <summary>The fixed label set behind the question cards on the Advisor page, and the source
/// used to build Claude's tool definitions for the free-text path (AskAdvisorCommand) - one shared
/// list so a question's label can never drift between the two entry points.</summary>
public static class AdvisorQuestions
{
    public static readonly IReadOnlyList<AdvisorQuestionDto> All =
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
}
