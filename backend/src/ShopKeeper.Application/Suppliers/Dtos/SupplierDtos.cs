namespace ShopKeeper.Application.Suppliers.Dtos;

public record SupplierDto(
    Guid Id,
    string Name,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive);

public record SupplierRestockDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    Guid BranchId,
    string BranchName,
    int Quantity,
    string CreatedByName,
    DateTimeOffset CreatedAt);
