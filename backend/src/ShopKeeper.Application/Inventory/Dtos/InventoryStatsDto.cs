namespace ShopKeeper.Application.Inventory.Dtos;

public record InventoryStatsDto(int TotalProducts, int LowStockCount, int OutOfStockCount, decimal InventoryValue);
