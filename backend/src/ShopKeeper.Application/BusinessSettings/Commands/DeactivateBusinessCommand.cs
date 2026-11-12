namespace ShopKeeper.Application.BusinessSettings.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Owner-initiated account closure. Deliberately a soft deactivation, not a delete:
/// Sale/SaleItem/InventoryTransaction are append-only ledgers (see CLAUDE.md), so erasing a
/// business's rows on request would destroy financial/audit history that may be legally
/// required later (tax, disputes). Business.IsActive is checked everywhere a token gets
/// minted or re-scoped (TokenIssuer, SwitchBusinessCommand, GetCurrentUserQuery), so a
/// deactivated business simply becomes permanently unreachable rather than being torn down.
/// Password-gated like DisableTwoFactorCommand - this is high-stakes enough to re-verify
/// identity, not just trust the still-valid access token. Reactivation is deliberately not
/// self-serve - it requires contacting support, the same way most SaaS "close my account"
/// flows work, so a compromised session can't be used to both lock out and later un-lock a
/// business without an operator noticing.
/// </summary>
public record DeactivateBusinessCommand(string Password) : IRequest;

public class DeactivateBusinessCommandValidator : AbstractValidator<DeactivateBusinessCommand>
{
    public DeactivateBusinessCommandValidator() => RuleFor(x => x.Password).NotEmpty();
}

public class DeactivateBusinessCommandHandler(IAppDbContext db, ICurrentUserService currentUser, IPasswordHasher hasher)
    : IRequestHandler<DeactivateBusinessCommand>
{
    public async Task Handle(DeactivateBusinessCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsOwner)
        {
            throw new ForbiddenAccessException("Only the business owner can deactivate the account.");
        }

        var userId = currentUser.RequireUserId();
        var user = await db.Users.FirstAsync(u => u.Id == userId, cancellationToken);

        if (!hasher.Verify(request.Password, user.PasswordHash))
        {
            throw new AuthenticationException("Incorrect password.");
        }

        var businessId = currentUser.RequireBusinessId();
        var business = await db.Businesses.FirstAsync(b => b.Id == businessId, cancellationToken);

        business.IsActive = false;

        // New tokens can never be minted for a deactivated business (see TokenIssuer/
        // SwitchBusinessCommand), but without this, a refresh token issued before
        // deactivation could still mint a *new* access token scoped to it for up to the
        // refresh token's own lifetime (days, not the 15-minute access-token tail). Revoking
        // every such token here closes that window immediately instead.
        var activeTokens = await db.RefreshTokens
            .Where(rt => rt.ActiveBusinessId == businessId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
            token.ReasonRevoked = "Business deactivated";
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
