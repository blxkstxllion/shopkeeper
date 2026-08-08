namespace ShopKeeper.Application.Auth.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;

public record VerifyEmailCommand(string Token) : IRequest;

public class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator() => RuleFor(x => x.Token).NotEmpty();
}

public class VerifyEmailCommandHandler(IAppDbContext db) : IRequestHandler<VerifyEmailCommand>
{
    public async Task Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.EmailVerificationToken == request.Token, cancellationToken);

        if (user is null || user.EmailVerificationExpiresAt is null || user.EmailVerificationExpiresAt < DateTimeOffset.UtcNow)
        {
            throw new AuthenticationException("This verification link is invalid or has expired.");
        }

        user.IsEmailVerified = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationExpiresAt = null;
        await db.SaveChangesAsync(cancellationToken);
    }
}
