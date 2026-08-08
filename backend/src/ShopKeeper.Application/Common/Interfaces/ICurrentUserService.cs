namespace ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Resolves the authenticated user and their active tenant context from the current
/// HTTP request's JWT claims. This is the single source of truth AppDbContext's
/// global query filters use to scope every tenant-owned query - see
/// AppDbContext.OnModelCreating.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? BusinessId { get; }
    Guid? BranchId { get; }
    bool IsOwner { get; }
    IReadOnlyCollection<string> Permissions { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }

    bool HasPermission(string permissionKey);
}
