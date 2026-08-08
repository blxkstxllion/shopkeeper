namespace ShopKeeper.Infrastructure.Identity;

using Microsoft.AspNetCore.Http;
using ShopKeeper.Application.Common.Interfaces;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private System.Security.Claims.ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? UserId => Guid.TryParse(User?.FindFirst("sub")?.Value, out var id) ? id : null;

    public Guid? BusinessId => Guid.TryParse(User?.FindFirst("business_id")?.Value, out var id) ? id : null;

    public Guid? BranchId => Guid.TryParse(User?.FindFirst("branch_id")?.Value, out var id) ? id : null;

    public bool IsOwner => User?.FindFirst("is_owner")?.Value == "true";

    public IReadOnlyCollection<string> Permissions =>
        User?.FindAll("permission").Select(c => c.Value).ToArray() ?? [];

    public string? IpAddress => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();

    public bool HasPermission(string permissionKey) => IsOwner || Permissions.Contains(permissionKey);
}
