namespace ShopKeeper.Infrastructure.Identity;

using Microsoft.AspNetCore.Http;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Common.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private System.Security.Claims.ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    // Only consulted when there's no HttpContext at all - a background job (ScheduledReportRunner),
    // never a real request, so this can never be used to bypass a real request's own JWT claims.
    private BackgroundJobPrincipal? JobContext => User is null ? BackgroundJobContext.CurrentPrincipal : null;

    public Guid? UserId => JobContext?.UserId ?? (Guid.TryParse(User?.FindFirst("sub")?.Value, out var id) ? id : null);

    public Guid? BusinessId => JobContext?.BusinessId ?? (Guid.TryParse(User?.FindFirst("business_id")?.Value, out var id) ? id : null);

    public Guid? BranchId => JobContext?.BranchId ?? (Guid.TryParse(User?.FindFirst("branch_id")?.Value, out var id) ? id : null);

    public bool IsOwner => JobContext is not null || User?.FindFirst("is_owner")?.Value == "true";

    public IReadOnlyCollection<string> Permissions =>
        JobContext is not null ? [] : User?.FindAll("permission").Select(c => c.Value).ToArray() ?? [];

    public string? IpAddress => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();

    public bool HasPermission(string permissionKey) => IsOwner || Permissions.Contains(permissionKey);
}
