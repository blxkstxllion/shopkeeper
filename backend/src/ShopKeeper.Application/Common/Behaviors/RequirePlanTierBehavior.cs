namespace ShopKeeper.Application.Common.Behaviors;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;

/// <summary>Marker for requests that need a plan-tier check before their handler runs (Reports,
/// AI Advisor). Opt-in like AuditLoggingBehavior's "*Command" convention, except here it's an
/// explicit interface rather than a name pattern, since only a handful of specific
/// queries/commands are plan-gated - most aren't.</summary>
public interface IRequirePlanFeature
{
    bool RequiresReports { get; }
    bool RequiresAi { get; }
}

/// <summary>
/// Runs before the handler (unlike AuditLoggingBehavior, which runs after) - a plan-tier
/// rejection should never let the handler's own work start. No-op for any request that doesn't
/// implement IRequirePlanFeature, so this is safe to register globally alongside the other
/// pipeline behaviors.
/// </summary>
public class RequirePlanTierBehavior<TRequest, TResponse>(IAppDbContext db, ICurrentUserService currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is IRequirePlanFeature feature)
        {
            var businessId = currentUser.RequireBusinessId();
            var tier = await db.Businesses.Where(b => b.Id == businessId).Select(b => b.PlanTier).FirstAsync(cancellationToken);
            var limits = PlanLimits.For(tier);

            if (feature.RequiresReports && !limits.HasReports)
            {
                throw new ForbiddenAccessException("Reports aren't available on your current plan. Upgrade to unlock them.");
            }
            if (feature.RequiresAi && !limits.HasAi)
            {
                throw new ForbiddenAccessException("The AI Advisor isn't available on your current plan. Upgrade to unlock it.");
            }
        }

        return await next();
    }
}
