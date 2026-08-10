namespace ShopKeeper.Application.Auth.Dtos;

public record SessionDto(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string? CreatedByIp,
    string? UserAgent,
    bool IsCurrent);
