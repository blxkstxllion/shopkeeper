namespace ShopKeeper.Application.Auth.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Entities;

/// <summary>Authenticated (unlike ForgotPasswordCommand) - the caller already holds a valid
/// access token for this user, so there's no account-enumeration concern to guard against here.</summary>
public record ResendVerificationEmailCommand(Guid UserId) : IRequest;

public class ResendVerificationEmailCommandHandler(IAppDbContext db, IEmailSender emailSender)
    : IRequestHandler<ResendVerificationEmailCommand>
{
    public async Task Handle(ResendVerificationEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        if (user.IsEmailVerified)
        {
            return;
        }

        user.EmailVerificationToken = Guid.NewGuid().ToString("N");
        user.EmailVerificationExpiresAt = DateTimeOffset.UtcNow.AddDays(2);
        await db.SaveChangesAsync(cancellationToken);

        await emailSender.SendEmailVerificationAsync(user.Email, user.FirstName, user.EmailVerificationToken, cancellationToken);
    }
}
