namespace ShopKeeper.Application.Expenses.Dtos;

public record ExpenseCategoryDto(Guid Id, string Name, string? Description, bool IsActive);

public record ExpenseDto(
    Guid Id,
    Guid? BranchId,
    string? BranchName,
    Guid ExpenseCategoryId,
    string CategoryName,
    decimal Amount,
    DateOnly ExpenseDate,
    string? Description,
    string CreatedByName,
    DateTimeOffset CreatedAt);
