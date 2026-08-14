namespace ShopKeeper.Application.AuditLogs.Dtos;

public record AuditLogDto(
    Guid Id,
    string Action,
    string? EntityType,
    Guid? EntityId,
    string? ActorName,
    string? PreviousValue,
    string? NewValue,
    string? IpAddress,
    DateTimeOffset CreatedAt);
