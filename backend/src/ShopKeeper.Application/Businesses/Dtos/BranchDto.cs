namespace ShopKeeper.Application.Businesses.Dtos;

public record BranchDto(Guid Id, string Name, string Code, string? City, bool IsMainBranch, bool IsActive);
