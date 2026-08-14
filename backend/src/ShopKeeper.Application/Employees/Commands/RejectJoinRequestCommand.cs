namespace ShopKeeper.Application.Employees.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

/// <summary>Leaves the dormant User row alone (soft, like everything else in this codebase) -
/// if they resubmit later, the email now belongs to a real account so it naturally routes
/// through the existing-user path instead of creating a duplicate.</summary>
public record RejectJoinRequestCommand(Guid JoinRequestId) : IRequest;

public class RejectJoinRequestCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<RejectJoinRequestCommand>
{
    public async Task Handle(RejectJoinRequestCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.EmployeesManage);
        var reviewerId = currentUser.RequireUserId();

        var joinRequest = await db.JoinRequests.FirstOrDefaultAsync(r => r.Id == request.JoinRequestId, cancellationToken)
            ?? throw new NotFoundException(nameof(JoinRequest), request.JoinRequestId);

        if (joinRequest.Status != JoinRequestStatus.Pending)
        {
            throw new ConflictException("This request has already been reviewed.");
        }

        joinRequest.Status = JoinRequestStatus.Rejected;
        joinRequest.ReviewedByUserId = reviewerId;
        joinRequest.ReviewedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }
}
