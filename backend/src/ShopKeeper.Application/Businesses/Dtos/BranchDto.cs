namespace ShopKeeper.Application.Businesses.Dtos;

public record BranchDto(
    Guid Id,
    string Name,
    string Code,
    string? Address,
    string? City,
    string? Country,
    string? Phone,
    string? Email,
    bool IsMainBranch,
    bool IsActive);
