namespace ShopKeeper.Application.Employees.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

/// <summary>
/// Always goes through the PendingInvitation + email path, even if a User with that email
/// already exists elsewhere - a person having an account on another business shouldn't
/// silently grant them access here without an explicit accept step (see
/// AcceptInvitationForExistingUserCommand for that step).
/// </summary>
public record InviteEmployeeCommand(string Email, Guid RoleId, Guid? BranchId) : IRequest<Guid>;

public class InviteEmployeeCommandValidator : AbstractValidator<InviteEmployeeCommand>
{
    public InviteEmployeeCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.RoleId).NotEmpty();
    }
}

public class InviteEmployeeCommandHandler(IAppDbContext db, ICurrentUserService currentUser, IEmailSender emailSender)
    : IRequestHandler<InviteEmployeeCommand, Guid>
{
    public async Task<Guid> Handle(InviteEmployeeCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.EmployeesManage);
        var businessId = currentUser.RequireBusinessId();
        var inviterId = currentUser.RequireUserId();

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), request.RoleId);

        if (role.Name == DefaultRoles.Owner && !currentUser.IsOwner)
        {
            throw new ForbiddenAccessException("Only an owner can invite someone as an owner.");
        }

        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (existingUser is not null)
        {
            var alreadyMember = await db.BusinessUsers
                .AnyAsync(bu => bu.UserId == existingUser.Id && bu.Status != BusinessUserStatus.Removed, cancellationToken);
            if (alreadyMember)
            {
                throw new ConflictException("This person is already a member of your team.");
            }
        }

        var alreadyInvited = await db.PendingInvitations
            .AnyAsync(i => i.Email == normalizedEmail && i.AcceptedAt == null, cancellationToken);
        if (alreadyInvited)
        {
            throw new ConflictException("An invitation is already pending for this email.");
        }

        var inviter = await db.Users.Where(u => u.Id == inviterId)
            .Select(u => new { u.FirstName, u.LastName }).FirstAsync(cancellationToken);
        var business = await db.Businesses.Where(b => b.Id == businessId)
            .Select(b => b.Name).FirstAsync(cancellationToken);

        var invitation = new PendingInvitation
        {
            BusinessId = businessId,
            Email = normalizedEmail,
            RoleId = request.RoleId,
            BranchId = request.BranchId,
            Token = Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            InvitedByUserId = inviterId,
        };

        db.PendingInvitations.Add(invitation);
        await db.SaveChangesAsync(cancellationToken);

        await emailSender.SendBusinessInviteAsync(
            normalizedEmail, business, $"{inviter.FirstName} {inviter.LastName}", invitation.Token, cancellationToken);

        return invitation.Id;
    }
}
