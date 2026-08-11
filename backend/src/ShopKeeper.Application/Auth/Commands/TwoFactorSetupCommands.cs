namespace ShopKeeper.Application.Auth.Commands;

using System.Security.Cryptography;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;

public record TwoFactorSetupDto(string Secret, string ProvisioningUri);

public record SetupTwoFactorCommand(Guid UserId) : IRequest<TwoFactorSetupDto>;

public class SetupTwoFactorCommandHandler(IAppDbContext db, ITotpService totp) : IRequestHandler<SetupTwoFactorCommand, TwoFactorSetupDto>
{
    public async Task<TwoFactorSetupDto> Handle(SetupTwoFactorCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.User), request.UserId);

        if (user.TwoFactorEnabled)
        {
            throw new ConflictException("Two-factor authentication is already enabled. Disable it first to set up a new device.");
        }

        var secret = totp.GenerateSecret();
        user.TwoFactorSecret = secret;
        await db.SaveChangesAsync(cancellationToken);

        return new TwoFactorSetupDto(secret, totp.BuildProvisioningUri(secret, user.Email));
    }
}

public record EnableTwoFactorCommand(Guid UserId, string Code) : IRequest<IReadOnlyList<string>>;

public class EnableTwoFactorCommandValidator : AbstractValidator<EnableTwoFactorCommand>
{
    public EnableTwoFactorCommandValidator() => RuleFor(x => x.Code).NotEmpty().Length(6);
}

/// <summary>Confirms setup with a real code from the authenticator app, then returns one-time
/// recovery codes - shown to the user exactly once, only the bcrypt hashes are ever stored.</summary>
public class EnableTwoFactorCommandHandler(IAppDbContext db, ITotpService totp, IPasswordHasher hasher)
    : IRequestHandler<EnableTwoFactorCommand, IReadOnlyList<string>>
{
    private const int RecoveryCodeCount = 8;

    public async Task<IReadOnlyList<string>> Handle(EnableTwoFactorCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.User), request.UserId);

        if (user.TwoFactorSecret is null)
        {
            throw new ConflictException("Start two-factor setup before confirming it.");
        }

        if (!totp.ValidateCode(user.TwoFactorSecret, request.Code))
        {
            throw new AuthenticationException("Invalid code. Check your authenticator app and try again.");
        }

        var recoveryCodes = Enumerable.Range(0, RecoveryCodeCount).Select(_ => GenerateRecoveryCode()).ToList();

        user.TwoFactorEnabled = true;
        user.TwoFactorRecoveryCodesJson = JsonSerializer.Serialize(recoveryCodes.Select(hasher.Hash).ToList());
        await db.SaveChangesAsync(cancellationToken);

        return recoveryCodes;
    }

    private static string GenerateRecoveryCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I - avoids transcription mistakes
        var bytes = RandomNumberGenerator.GetBytes(10);
        var chars = bytes.Select(b => alphabet[b % alphabet.Length]).ToArray();
        return $"{new string(chars[..5])}-{new string(chars[5..])}";
    }
}

public record DisableTwoFactorCommand(Guid UserId, string Password) : IRequest;

public class DisableTwoFactorCommandHandler(IAppDbContext db, IPasswordHasher hasher) : IRequestHandler<DisableTwoFactorCommand>
{
    public async Task Handle(DisableTwoFactorCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.User), request.UserId);

        if (!hasher.Verify(request.Password, user.PasswordHash))
        {
            throw new AuthenticationException("Incorrect password.");
        }

        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.TwoFactorRecoveryCodesJson = null;
        await db.SaveChangesAsync(cancellationToken);
    }
}
