namespace ShopKeeper.Application.Employees.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Auth.Dtos;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

/// <summary>
/// Authenticated counterpart to AcceptInvitationCommand - for someone who already has an
/// account (on this business or elsewhere), so they log in first, then hit this action instead
/// of registering again. Re-issues tokens scoped to the newly-joined business, same as
/// SwitchBusinessCommand, so they land straight in it.
/// </summary>
public record AcceptInvitationForExistingUserCommand(string Token, string? IpAddress, string? UserAgent = null)
    : IRequest<AuthResultDto>;

public class AcceptInvitationForExistingUserCommandValidator : AbstractValidator<AcceptInvitationForExistingUserCommand>
{
    public AcceptInvitationForExistingUserCommandValidator() => RuleFor(x => x.Token).NotEmpty();
}

public class AcceptInvitationForExistingUserCommandHandler(IAppDbContext db, ICurrentUserService currentUser, TokenIssuer tokenIssuer)
    : IRequestHandler<AcceptInvitationForExistingUserCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(AcceptInvitationForExistingUserCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var invitation = await db.PendingInvitations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Token == request.Token, cancellationToken)
            ?? throw new NotFoundException(nameof(PendingInvitation), request.Token);

        if (invitation.AcceptedAt is not null)
        {
            throw new ConflictException("This invitation has already been accepted.");
        }

        if (invitation.ExpiresAt < DateTimeOffset.UtcNow)
        {
            throw new ConflictException("This invitation has expired.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        if (!string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException("This invitation was sent to a different email address.");
        }

        var alreadyMember = await db.BusinessUsers.IgnoreQueryFilters()
            .AnyAsync(bu => bu.UserId == userId && bu.BusinessId == invitation.BusinessId && bu.Status != BusinessUserStatus.Removed, cancellationToken);
        if (alreadyMember)
        {
            throw new ConflictException("You are already a member of this business.");
        }

        // IgnoreQueryFilters: the caller's active tenant context may be a different business
        // than the one they're accepting an invite into - invitation.BusinessId is already a
        // trusted, validated value from the token lookup above.
        var roleName = await db.Roles.IgnoreQueryFilters()
            .Where(r => r.Id == invitation.RoleId).Select(r => r.Name).FirstAsync(cancellationToken);

        db.BusinessUsers.Add(new BusinessUser
        {
            BusinessId = invitation.BusinessId,
            UserId = user.Id,
            User = user,
            RoleId = invitation.RoleId,
            BranchId = invitation.BranchId,
            IsOwner = roleName == DefaultRoles.Owner,
            Status = BusinessUserStatus.Active,
            JoinedAt = DateTimeOffset.UtcNow,
        });

        invitation.AcceptedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return await tokenIssuer.IssueAsync(user, invitation.BusinessId, request.IpAddress, request.UserAgent, cancellationToken);
    }
}
