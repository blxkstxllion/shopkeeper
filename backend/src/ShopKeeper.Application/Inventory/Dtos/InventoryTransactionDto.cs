namespace ShopKeeper.Application.Inventory.Dtos;

public record InventoryTransactionDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    Guid BranchId,
    string BranchName,
    string Type,
    int QuantityChange,
    int QuantityAfter,
    string Reason,
    string? ReferenceType,
    Guid? ReferenceId,
    string CreatedByName,
    DateTimeOffset CreatedAt);
