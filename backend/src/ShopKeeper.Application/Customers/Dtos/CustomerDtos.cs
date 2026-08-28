namespace ShopKeeper.Application.Customers.Dtos;

public record CustomerDto(
    Guid Id,
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive);

/// <summary>
/// TotalSpend/AverageSale/LastPurchaseAt are real aggregates over the customer's Sale history -
/// "lifetime spend to date", not a predictive lifetime-value model. Voided sales are excluded,
/// matching how they're excluded from every other revenue figure in this app (Dashboard, Reports).
/// </summary>
public record CustomerDetailDto(
    Guid Id,
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive,
    decimal TotalSpend,
    decimal AverageSale,
    int PurchaseCount,
    DateTimeOffset? LastPurchaseAt);
