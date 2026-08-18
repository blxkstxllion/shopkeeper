namespace ShopKeeper.Application.Roles.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Roles.Dtos;
using ShopKeeper.Domain.Enums;

public record GetRoleManagementQuery : IRequest<IReadOnlyList<RoleManagementDto>>;

/// <summary>Viewing role definitions is not itself owner/permission-gated (matches
/// GetPlanUsageQuery's precedent) - only creating, editing, and deleting roles are
/// restricted. This is the full-detail counterpart to Employees.GetRolesQuery (which
/// stays id+name only for the invite-employee picker, unchanged).</summary>
public class GetRoleManagementQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetRoleManagementQuery, IReadOnlyList<RoleManagementDto>>
{
    public async Task<IReadOnlyList<RoleManagementDto>> Handle(GetRoleManagementQuery request, CancellationToken cancellationToken)
    {
        var businessId = currentUser.RequireBusinessId();

        var roles = await db.Roles
            .Where(r => r.BusinessId == businessId && r.IsActive)
            .OrderBy(r => r.Name)
            .Select(r => new RoleManagementDto(
                r.Id,
                r.Name,
                r.Description,
                r.IsSystemRole,
                r.RolePermissions.Select(rp => rp.Permission.Key).ToList(),
                r.BusinessUsers.Count(bu => bu.Status != BusinessUserStatus.Removed)))
            .ToListAsync(cancellationToken);

        return roles;
    }
}
