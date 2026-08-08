namespace ShopKeeper.Api.Tests.TestHelpers;

using ShopKeeper.Application.Common.Interfaces;

/// <summary>Mutable stand-in for the real claims-based ICurrentUserService, so tests can set tenant context directly.</summary>
public class TestCurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; set; }
    public Guid? BusinessId { get; set; }
    public Guid? BranchId { get; set; }
    public bool IsOwner { get; set; }
    public List<string> PermissionsList { get; set; } = [];
    public IReadOnlyCollection<string> Permissions => PermissionsList;
    public string? IpAddress => "127.0.0.1";
    public string? UserAgent => "xunit-test-runner";

    public bool HasPermission(string permissionKey) => IsOwner || PermissionsList.Contains(permissionKey);
}
