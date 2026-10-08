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
    public async Task<bool> Handle(UnlockLocalAppCommand request, CancellationToken cancellationToken)
    {
        var settings = await db.LocalAppSettings.FirstOrDefaultAsync(cancellationToken)
            ?? throw new AuthenticationException("Setup has not been completed yet.");

        return hasher.Verify(request.Pin, settings.PinHash);
    }
}
