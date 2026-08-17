namespace ShopKeeper.Application.Employees.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

/// <summary>Creates the real BusinessUser - the same tail-end shape as AcceptInvitationCommand's
/// membership creation, just fed by a JoinRequest instead of a PendingInvitation.</summary>
public record ApproveJoinRequestCommand(Guid JoinRequestId, Guid RoleId, Guid? BranchId) : IRequest;

public class ApproveJoinRequestCommandValidator : AbstractValidator<ApproveJoinRequestCommand>
{
    public ApproveJoinRequestCommandValidator() => RuleFor(x => x.RoleId).NotEmpty();
}

public class ApproveJoinRequestCommandHandler(IAppDbContext db, ICurrentUserService currentUser, PlanLimitService planLimits)
    : IRequestHandler<ApproveJoinRequestCommand>
{
    public async Task Handle(ApproveJoinRequestCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.EmployeesManage);
        var reviewerId = currentUser.RequireUserId();

        var joinRequest = await db.JoinRequests.FirstOrDefaultAsync(r => r.Id == request.JoinRequestId, cancellationToken)
            ?? throw new NotFoundException(nameof(JoinRequest), request.JoinRequestId);

        if (joinRequest.Status != JoinRequestStatus.Pending)
        {
            throw new ConflictException("This request has already been reviewed.");
        }

        await planLimits.EnsureCanAddStaffAsync(joinRequest.BusinessId, cancellationToken);

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), request.RoleId);

        if (role.Name == DefaultRoles.Owner && !currentUser.IsOwner)
        {
            throw new ForbiddenAccessException("Only an owner can approve someone as an owner.");
        }

        db.BusinessUsers.Add(new BusinessUser
        {
            BusinessId = joinRequest.BusinessId,
            UserId = joinRequest.UserId,
            RoleId = role.Id,
            BranchId = request.BranchId,
            IsOwner = role.Name == DefaultRoles.Owner,
            Status = BusinessUserStatus.Active,
            JoinedAt = DateTimeOffset.UtcNow,
        });

        joinRequest.Status = JoinRequestStatus.Approved;
        joinRequest.ReviewedByUserId = reviewerId;
        joinRequest.ReviewedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }
}
