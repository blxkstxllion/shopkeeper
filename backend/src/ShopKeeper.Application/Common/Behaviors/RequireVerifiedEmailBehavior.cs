namespace ShopKeeper.Application.Common.Behaviors;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Blocks every command/query for a user whose email verification is both unverified and
/// enforced (see User.EmailVerificationEnforced) - opt-OUT rather than opt-in like
/// RequirePlanTierBehavior, since the intent here is "block essentially everything until
/// verified," not "block a specific handful of features." The exemption is by namespace
/// (ShopKeeper.Application.Auth.*, .Onboarding.Commands) rather than a marker interface on ~17
/// individual commands/queries, so a new auth command added later is automatically exempt
/// without anyone having to remember to opt it out. Onboarding is exempted too because the
/// frontend's own route guard (AppRouter's RequireVerifiedEmail) only wraps /app/*, not
/// /onboarding - a brand-new, unverified user must still be able to create their business
/// before hitting the verification wall, or registration would be a dead end.
///
/// Runs before the handler, like RequirePlanTierBehavior - a verification rejection should
/// never let the handler's own work start. No-op for unauthenticated requests (nothing to
/// check yet) and for already-verified/not-enforced users, so this is cheap in the common case.
/// </summary>
public class RequireVerifiedEmailBehavior<TRequest, TResponse>(IAppDbContext db, ICurrentUserService currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var ns = typeof(TRequest).Namespace;
        var isExempt = ns is "ShopKeeper.Application.Auth.Commands" or "ShopKeeper.Application.Auth.Queries"
            or "ShopKeeper.Application.Onboarding.Commands";

        if (!isExempt && currentUser.UserId is Guid userId)
        {
            var blocked = await db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.EmailVerificationEnforced && !u.IsEmailVerified)
                .FirstOrDefaultAsync(cancellationToken);

            if (blocked)
            {
                throw new ForbiddenAccessException("Please verify your email address to continue using The Shop Keeper.");
            }
        }

        return await next();
    }
}
