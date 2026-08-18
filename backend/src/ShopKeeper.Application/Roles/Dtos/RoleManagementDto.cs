namespace ShopKeeper.Application.Roles.Dtos;

public record RoleManagementDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    IReadOnlyList<string> PermissionKeys,
    int EmployeeCount);

public record PermissionCatalogItemDto(string Key, string Name, string Category);
