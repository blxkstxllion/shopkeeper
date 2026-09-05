namespace ShopKeeper.Application.Auth.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Auth.Dtos;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Domain.Entities;

public record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? IpAddress,
    string? UserAgent = null) : IRequest<AuthResultDto>;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one number.");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
    }
}

public class RegisterCommandHandler(IAppDbContext db, IPasswordHasher hasher, TokenIssuer tokenIssuer)
    : IRequestHandler<RegisterCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var exists = await db.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (exists)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = hasher.Hash(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            IsEmailVerified = false,
            // Enforcement (and sending the verification email below) is switched off for
            // now - AWS SES for this account is stuck in sandbox mode, so a real verification
            // email can never actually reach a new user. Re-enable both once SES production
            // access is granted, or every sign-up would be gated on an email nobody receives.
            EmailVerificationEnforced = false,
        };

        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The precheck above is the fast path; this is the race-window safety net. Only
            // convert to the same ConflictException when the specific condition it protects is
            // confirmed true after the fact - anything else rethrows unchanged and still
            // surfaces as a logged 500, exactly as an unrelated DbUpdateException should.
            var stillExists = await db.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
            if (!stillExists)
            {
                throw;
            }

            throw new ConflictException("An account with this email already exists.");
        }

        return await tokenIssuer.IssueAsync(user, activeBusinessId: null, rememberMe: false, request.IpAddress, request.UserAgent, cancellationToken);
    }
}
