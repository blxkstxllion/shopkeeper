namespace ShopKeeper.Application.Employees.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

/// <summary>Authenticated counterpart to SubmitJoinRequestCommand - for someone who already has
/// an account (on this business or elsewhere) using the shared join code instead of a targeted
/// invite.</summary>
public record SubmitJoinRequestForExistingUserCommand(string Code) : IRequest;

public class SubmitJoinRequestForExistingUserCommandValidator : AbstractValidator<SubmitJoinRequestForExistingUserCommand>
{
    public SubmitJoinRequestForExistingUserCommandValidator() => RuleFor(x => x.Code).NotEmpty();
}

public class SubmitJoinRequestForExistingUserCommandHandler(
    IAppDbContext db, ICurrentUserService currentUser, NotificationDispatcher notifications)
    : IRequestHandler<SubmitJoinRequestForExistingUserCommand>
{
    public async Task Handle(SubmitJoinRequestForExistingUserCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var setting = await db.BusinessSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.JoinCode == request.Code, cancellationToken)
            ?? throw new NotFoundException(nameof(BusinessSetting), request.Code);

        var alreadyMember = await db.BusinessUsers.IgnoreQueryFilters()
            .AnyAsync(bu => bu.UserId == userId && bu.BusinessId == setting.BusinessId && bu.Status != BusinessUserStatus.Removed, cancellationToken);
        if (alreadyMember)
        {
            throw new ConflictException("You are already a member of this business.");
        }

        var alreadyPending = await db.JoinRequests.IgnoreQueryFilters()
            .AnyAsync(r => r.UserId == userId && r.BusinessId == setting.BusinessId && r.Status == JoinRequestStatus.Pending, cancellationToken);
        if (alreadyPending)
        {
            throw new ConflictException("You already have a pending request to join this business.");
        }

        db.JoinRequests.Add(new JoinRequest
        {
            BusinessId = setting.BusinessId,
            UserId = userId,
        });

        var requester = await db.Users.Where(u => u.Id == userId)
            .Select(u => new { u.FirstName, u.LastName }).FirstAsync(cancellationToken);
        await notifications.NotifyOwnersAsync(
            setting.BusinessId, "JoinRequestSubmitted", "New join request",
            $"{requester.FirstName} {requester.LastName} wants to join your team.", "/app/employees", cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The precheck above is the fast path; this is the race-window safety net, backed by
            // JoinRequestConfiguration's partial unique index on (BusinessId, UserId) WHERE
            // Status = 'Pending'. Only convert when the specific condition is confirmed true
            // after the fact - anything else rethrows unchanged.
            var stillPending = await db.JoinRequests.IgnoreQueryFilters()
                .AnyAsync(r => r.UserId == userId && r.BusinessId == setting.BusinessId && r.Status == JoinRequestStatus.Pending, cancellationToken);
            if (!stillPending)
            {
                throw;
            }

            throw new ConflictException("You already have a pending request to join this business.");
        }
    }
}
