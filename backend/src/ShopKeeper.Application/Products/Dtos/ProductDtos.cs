namespace ShopKeeper.Application.Products.Dtos;

public record ProductCategoryDto(Guid Id, string Name, string? Description, bool IsActive);

public record ProductDto(
    Guid Id,
    string Name,
    string Sku,
    string? Barcode,
    string? Description,
    string? ImageUrl,
    Guid? CategoryId,
    string? CategoryName,
    Guid? SupplierId,
    string? SupplierName,
    decimal SellingPrice,
    decimal CostPrice,
    int MinStock,
    int ReorderLevel,
    bool TrackInventory,
    bool IsActive,
    int? QuantityOnHand,
    bool IsLowStock);
