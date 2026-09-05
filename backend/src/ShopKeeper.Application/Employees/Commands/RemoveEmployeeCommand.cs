namespace ShopKeeper.Application.Employees.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

/// <summary>Soft removal (Status = Removed) rather than a hard delete, so their historical
/// sales/audit trail stays attributable. The business's last active owner can never be removed -
/// that would leave the business with nobody able to manage it.</summary>
public record RemoveEmployeeCommand(Guid BusinessUserId, Guid? ClientRequestId = null) : IRequest, ISupportsClientRequestId;

public class RemoveEmployeeCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<RemoveEmployeeCommand>
{
    public async Task Handle(RemoveEmployeeCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.EmployeesManage);

        var member = await db.BusinessUsers.FirstOrDefaultAsync(bu => bu.Id == request.BusinessUserId, cancellationToken)
            ?? throw new NotFoundException(nameof(BusinessUser), request.BusinessUserId);

        if (member.IsOwner)
        {
            if (!currentUser.IsOwner)
            {
                throw new ForbiddenAccessException("Only an owner can remove an owner.");
            }

            var otherActiveOwners = await db.BusinessUsers.AnyAsync(
                bu => bu.Id != member.Id && bu.IsOwner && bu.Status == BusinessUserStatus.Active, cancellationToken);
            if (!otherActiveOwners)
            {
                throw new ConflictException("Cannot remove the only owner of this business.");
            }
        }

        member.Status = BusinessUserStatus.Removed;
        await db.SaveChangesAsync(cancellationToken);
    }
}
