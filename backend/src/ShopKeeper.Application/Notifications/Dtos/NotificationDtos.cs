namespace ShopKeeper.Application.Notifications.Dtos;

public record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Message,
    string? Link,
    bool IsRead,
    DateTimeOffset CreatedAt);
