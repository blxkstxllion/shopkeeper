namespace ShopKeeper.Application.Reports.Dtos;

public record InventoryValuationDto(int TotalProducts, int LowStockCount, int OutOfStockCount, decimal InventoryValue);

public record StockAlertProductDto(Guid ProductId, string ProductName, int QuantityOnHand, int MinimumStock);

/// <summary>
/// TurnoverRatio is UnitsSoldInRange / current QuantityOnHand - an approximation, not a true
/// time-averaged turnover, since stock is a point-in-time snapshot with no historical series to
/// average over. Null when there's no current stock to divide by (can't turn over zero inventory).
/// </summary>
public record ProductTurnoverDto(Guid ProductId, string ProductName, int UnitsSoldInRange, int QuantityOnHand, decimal? TurnoverRatio);

public record InventoryReportDto(
    InventoryValuationDto Valuation,
    IReadOnlyList<StockAlertProductDto> LowStockProducts,
    IReadOnlyList<StockAlertProductDto> OutOfStockProducts,
    IReadOnlyList<ProductTurnoverDto> Turnover);
