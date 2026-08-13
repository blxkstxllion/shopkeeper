namespace ShopKeeper.Application.Employees.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Employees.Dtos;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Enums;

/// <summary>Shows current members alongside outstanding invitations so the Employees page
/// renders one coherent list, not two separate screens.</summary>
public record GetBusinessUsersQuery : IRequest<BusinessUsersDto>;

public class GetBusinessUsersQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetBusinessUsersQuery, BusinessUsersDto>
{
    public async Task<BusinessUsersDto> Handle(GetBusinessUsersQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.EmployeesManage);

        var members = await db.BusinessUsers
            .Where(bu => bu.Status != BusinessUserStatus.Removed)
            .Include(bu => bu.User)
            .Include(bu => bu.Role)
            .Include(bu => bu.Branch)
            .OrderBy(bu => bu.User.FirstName)
            .Select(bu => new BusinessMemberDto(
                bu.Id, bu.UserId, bu.User.FirstName, bu.User.LastName, bu.User.Email,
                bu.Role.Name, bu.Branch != null ? bu.Branch.Name : null,
                bu.Status.ToString(), bu.IsOwner, bu.JoinedAt))
            .ToListAsync(cancellationToken);

        var pending = await db.PendingInvitations
            .Where(i => i.AcceptedAt == null)
            .Include(i => i.Role)
            .Include(i => i.Branch)
            .OrderBy(i => i.Email)
            .Select(i => new PendingInvitationDto(
                i.Id, i.Email, i.Role.Name, i.Branch != null ? i.Branch.Name : null, i.CreatedAt, i.ExpiresAt))
            .ToListAsync(cancellationToken);

        return new BusinessUsersDto(members, pending);
    }
}
