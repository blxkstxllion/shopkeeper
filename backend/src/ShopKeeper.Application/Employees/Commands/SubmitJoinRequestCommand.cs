namespace ShopKeeper.Application.Employees.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Entities;

/// <summary>
/// Unauthenticated - a worker scanning/typing a shared join code to ask to join a business.
/// Creates their User account immediately (per the confirmed design: one interaction, no
/// follow-up contact needed - they can already try logging in and see "waiting for approval").
/// Unlike AcceptInvitationCommand, holding this code is NOT proof of email ownership (it's a
/// shared code, not a link mailed to one address), so IsEmailVerified stays false here -
/// matches RegisterCommand's default. No tokens are issued: nothing should be usable until
/// the owner approves and a real BusinessUser row exists.
/// </summary>
public record SubmitJoinRequestCommand(
    string Code, string FirstName, string LastName, string Email, string Phone, string Password) : IRequest;

public class SubmitJoinRequestCommandValidator : AbstractValidator<SubmitJoinRequestCommand>
{
    public SubmitJoinRequestCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one number.");
    }
}

public class SubmitJoinRequestCommandHandler(IAppDbContext db, IPasswordHasher hasher) : IRequestHandler<SubmitJoinRequestCommand>
{
    public async Task Handle(SubmitJoinRequestCommand request, CancellationToken cancellationToken)
    {
        var setting = await db.BusinessSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.JoinCode == request.Code, cancellationToken)
            ?? throw new NotFoundException(nameof(BusinessSetting), request.Code);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (existingUser is not null)
        {
            throw new ConflictException("An account with this email already exists. Please log in to request to join.");
        }

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = hasher.Hash(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = request.Phone.Trim(),
            IsEmailVerified = false,
        };
        db.Users.Add(user);

        db.JoinRequests.Add(new JoinRequest
        {
            BusinessId = setting.BusinessId,
            UserId = user.Id,
            User = user,
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
