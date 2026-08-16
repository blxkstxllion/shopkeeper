namespace ShopKeeper.Application.Advisor;

/// <summary>A closed set of questions, not free text - this is a calculation-based advisor, not
/// an LLM, so "understanding" a question means routing a known id to a known calculation. Each
/// value maps to one branch in GetAdvisorAnswerQueryHandler.Handle.</summary>
public enum AdvisorQuestionId
{
    RevenueThisMonth,
    ProfitMargin,
    LowStock,
    BestSellingProduct,
    WorstPerformingProduct,
    BranchComparison,
    TopExpenseCategories,
    AmIProfitable,
}
