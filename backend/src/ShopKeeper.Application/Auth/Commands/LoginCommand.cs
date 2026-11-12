namespace ShopKeeper.Application.Auth.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Auth.Dtos;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Common.Services;

public record LoginCommand(
    string Email, string Password, Guid? BusinessId, bool RememberMe, string? IpAddress, string? UserAgent = null)
    : IRequest<LoginResultDto>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginCommandHandler(IAppDbContext db, IPasswordHasher hasher, TokenIssuer tokenIssuer, IJwtTokenService jwt)
    : IRequestHandler<LoginCommand, LoginResultDto>
{
    // Same thresholds as VerifyTwoFactorCommand's lockout, for the same reasoning: the per-IP
    // rate limit bounds one source, this bounds one targeted account across many sources.
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<LoginResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is not null)
        {
            var now = DateTimeOffset.UtcNow;
            if (user.LoginLockedUntil is { } lockedUntil && lockedUntil > now)
            {
                var minutesRemaining = Math.Max(1, (int)Math.Ceiling((lockedUntil - now).TotalMinutes));
                throw new AuthenticationException($"Too many failed attempts. Try again in {minutesRemaining} minute(s).");
            }
        }

        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
        {
            if (user is not null)
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= MaxFailedAttempts)
                {
                    user.LoginLockedUntil = DateTimeOffset.UtcNow.Add(LockoutDuration);
                }

                await db.SaveChangesAsync(cancellationToken);
            }

            throw new AuthenticationException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            throw new AuthenticationException("This account has been deactivated.");
        }

        user.FailedLoginAttempts = 0;
        user.LoginLockedUntil = null;
        user.LastLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        if (user.TwoFactorEnabled)
        {
            // Password check passed, but no tokens are issued yet - see LoginResultDto.
            var challengeToken = jwt.GenerateTwoFactorChallengeToken(user.Id, request.BusinessId, request.RememberMe);
            return new LoginResultDto(true, challengeToken, null);
        }

        var auth = await tokenIssuer.IssueAsync(user, request.BusinessId, request.RememberMe, request.IpAddress, request.UserAgent, cancellationToken);
        return new LoginResultDto(false, null, auth);
    }
}
