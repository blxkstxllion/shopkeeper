namespace ShopKeeper.Application.Reports.Dtos;

public record ExpenseCategoryTotalDto(string CategoryName, decimal Amount, decimal PercentOfTotal);

public record DailyExpensePointDto(DateOnly Date, decimal Amount);

public record ExpenseReportDto(
    decimal TotalAmount,
    IReadOnlyList<ExpenseCategoryTotalDto> ByCategory,
    IReadOnlyList<DailyExpensePointDto> DailyTrend);
