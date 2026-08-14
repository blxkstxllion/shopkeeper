namespace ShopKeeper.Application.Employees.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Auth.Dtos;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

/// <summary>
/// Unauthenticated - creates a brand-new User for someone who doesn't have an account yet.
/// Mirrors RegisterCommand's password/email validation and TokenIssuer usage, but also creates
/// the BusinessUser membership and marks the invitation accepted, all in one step, so the
/// invitee lands straight in the app instead of registering then joining separately.
/// </summary>
public record AcceptInvitationCommand(
    string Token, string Password, string FirstName, string LastName, string? IpAddress, string? UserAgent = null)
    : IRequest<AuthResultDto>;

public class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one number.");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
    }
}

public class AcceptInvitationCommandHandler(IAppDbContext db, IPasswordHasher hasher, TokenIssuer tokenIssuer)
    : IRequestHandler<AcceptInvitationCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(AcceptInvitationCommand request, CancellationToken cancellationToken)
    {
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

        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Email == invitation.Email, cancellationToken);
        if (existingUser is not null)
        {
            throw new ConflictException("An account with this email already exists. Please log in to accept this invitation.");
        }

        // IgnoreQueryFilters: this request is unauthenticated (no tenant context yet), so the
        // normal BusinessId == TenantBusinessId filter would exclude every Role, not just other
        // tenants' - invitation.BusinessId is already a trusted, validated value from the token
        // lookup above.
        var roleName = await db.Roles.IgnoreQueryFilters()
            .Where(r => r.Id == invitation.RoleId).Select(r => r.Name).FirstAsync(cancellationToken);

        var user = new User
        {
            Email = invitation.Email,
            PasswordHash = hasher.Hash(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            IsEmailVerified = true, // holding a valid invite token to this address is proof of ownership
        };
        db.Users.Add(user);

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
