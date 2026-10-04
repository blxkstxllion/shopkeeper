namespace ShopKeeper.Application.Customers.Dtos;

public record CustomerDto(
    Guid Id,
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive,
    decimal CurrentBalance,
    // Exposed so the frontend can pass it back on RefundSaleCommand's ApplyToBalance split -
    // the server rejects a stale value rather than silently applying a split calculated
    // against a balance that's since moved. See RefundSaleCommand's own doc comment.
    int BalanceRowVersion);

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
    DateTimeOffset? LastPurchaseAt,
    decimal CurrentBalance,
    int BalanceRowVersion);

public record CustomerLedgerEntryDto(
    Guid Id,
    string Type,
    decimal Amount,
    decimal BalanceAfter,
    string ReferenceType,
    Guid ReferenceId,
    string? Method,
    string? ReferenceNumber,
    string? Note,
    DateTimeOffset CreatedAt);
