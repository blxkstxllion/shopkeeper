namespace ShopKeeper.Application.Auth.Commands;

using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Auth.Dtos;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Domain.Entities;

/// <summary>Completes a login that was paused for 2FA - see LoginCommand and LoginResultDto.</summary>
public record VerifyTwoFactorCommand(string ChallengeToken, string Code, string? IpAddress, string? UserAgent = null)
    : IRequest<AuthResultDto>;

public class VerifyTwoFactorCommandValidator : AbstractValidator<VerifyTwoFactorCommand>
{
    public VerifyTwoFactorCommandValidator()
    {
        RuleFor(x => x.ChallengeToken).NotEmpty();
        RuleFor(x => x.Code).NotEmpty();
    }
}

public class VerifyTwoFactorCommandHandler(
    IAppDbContext db, IJwtTokenService jwt, ITotpService totp, IPasswordHasher hasher, TokenIssuer tokenIssuer)
    : IRequestHandler<VerifyTwoFactorCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(VerifyTwoFactorCommand request, CancellationToken cancellationToken)
    {
        var challenge = jwt.ValidateTwoFactorChallengeToken(request.ChallengeToken)
            ?? throw new AuthenticationException("This verification session has expired. Please log in again.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == challenge.UserId, cancellationToken)
            ?? throw new AuthenticationException("This verification session is no longer valid.");

        if (!user.TwoFactorEnabled || user.TwoFactorSecret is null)
        {
            throw new AuthenticationException("Two-factor authentication is not enabled for this account.");
        }

        var isValidTotp = totp.ValidateCode(user.TwoFactorSecret, request.Code);
        var isValidRecoveryCode = !isValidTotp && TryConsumeRecoveryCode(user, request.Code);

        if (!isValidTotp && !isValidRecoveryCode)
        {
            throw new AuthenticationException("Invalid verification code.");
        }

        if (isValidRecoveryCode)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return await tokenIssuer.IssueAsync(user, challenge.BusinessId, challenge.RememberMe, request.IpAddress, request.UserAgent, cancellationToken);
    }

    private bool TryConsumeRecoveryCode(User user, string code)
    {
        if (user.TwoFactorRecoveryCodesJson is null)
        {
            return false;
        }

        var hashes = JsonSerializer.Deserialize<List<string>>(user.TwoFactorRecoveryCodesJson) ?? [];
        var matchIndex = hashes.FindIndex(h => hasher.Verify(code, h));

        if (matchIndex < 0)
        {
            return false;
        }

        hashes.RemoveAt(matchIndex);
        user.TwoFactorRecoveryCodesJson = JsonSerializer.Serialize(hashes);
        return true;
    }
}
