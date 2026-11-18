namespace ShopKeeper.Application.LocalEdition.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;

/// <summary>Offline-edition-only. Checks the app-lock PIN - this is a UI gate, not an API
/// authorization boundary (see LocalAuthenticationHandler's doc comment: every request is already
/// authenticated as the single local owner regardless of PIN state). A wrong PIN just means the
/// frontend keeps showing the lock screen instead of the app shell.</summary>
public record UnlockLocalAppCommand(string Pin) : IRequest<bool>;

public class UnlockLocalAppCommandValidator : AbstractValidator<UnlockLocalAppCommand>
{
    public UnlockLocalAppCommandValidator()
    {
        RuleFor(x => x.Pin).NotEmpty();
    }
}

public class UnlockLocalAppCommandHandler(IAppDbContext db, IPasswordHasher hasher)
    : IRequestHandler<UnlockLocalAppCommand, bool>
{
    // A 4-digit PIN is only 10,000 combinations - without a lockout, an unattended app left
    // open could be brute-forced in well under a minute. Same thresholds as User's login
    // lockout, for the same reasoning.
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<bool> Handle(UnlockLocalAppCommand request, CancellationToken cancellationToken)
    {
        var settings = await db.LocalAppSettings.FirstOrDefaultAsync(cancellationToken)
            ?? throw new AuthenticationException("Setup has not been completed yet.");

        var now = DateTimeOffset.UtcNow;
        if (settings.UnlockLockedUntil is { } lockedUntil && lockedUntil > now)
        {
            return false;
        }

        var isCorrect = hasher.Verify(request.Pin, settings.PinHash);

        if (!isCorrect)
        {
            settings.FailedUnlockAttempts++;
            if (settings.FailedUnlockAttempts >= MaxFailedAttempts)
            {
                settings.UnlockLockedUntil = now.Add(LockoutDuration);
            }
        }
        else
        {
            settings.FailedUnlockAttempts = 0;
            settings.UnlockLockedUntil = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return isCorrect;
    }
}
