namespace ShopKeeper.Application.Common.Behaviors;

using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Entities;

/// <summary>Opt-in marker for commands whose write should be safely replayable - specifically,
/// mutations queued offline (frontend/src/offline) and retried against the real API once the
/// connection returns. Exposes the client-generated key so a retry after a *lost response* (not
/// a failed request) returns the original result instead of creating a duplicate.</summary>
public interface ISupportsClientRequestId
{
    Guid? ClientRequestId { get; }
}

/// <summary>
/// Generalizes the idempotency pattern CreateSaleCommand pioneered (precheck by
/// (BusinessId, ClientRequestId), catch a unique-constraint race, return the winner) into one
/// shared behavior instead of hand-rolling it per command, backed by one shared
/// IdempotencyKeys table instead of a bespoke partial-unique-index per entity.
/// CreateSaleCommand deliberately keeps its own original mechanism rather than switching to
/// this one - it's already proven, and Sale's own partial unique index protects the Sale row
/// itself, which this generic version does not (see below).
///
/// Weaker than Sales' bespoke version in one specific way: the response is persisted to
/// IdempotencyKeys *after* the handler's own SaveChangesAsync already committed, as a separate
/// round trip - so two genuinely concurrent replays of the identical ClientRequestId could both
/// execute the handler before either's IdempotencyKeys row lands, producing two entities instead
/// of one. Accepted as sufficient for the actual usage pattern this exists for: a single
/// device's offline sync loop (useSyncQueue.ts) replays its own queued mutations sequentially,
/// one at a time, never in parallel - the real scenario this protects against is a retried sync
/// after a lost response, not a race between simultaneous duplicate submissions.
///
/// No-op for any request that doesn't implement ISupportsClientRequestId or leaves it null, so
/// this is safe to register globally.
///
/// `where TRequest : notnull`, not `where TRequest : IRequest{TResponse}` - see the comment on
/// ValidationBehavior for why the more-specific-looking constraint silently breaks this behavior
/// for every void IRequest command.
/// </summary>
public class IdempotencyBehavior<TRequest, TResponse>(IAppDbContext db, ICurrentUserService currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ISupportsClientRequestId { ClientRequestId: { } clientRequestId } || currentUser.BusinessId is not { } businessId)
        {
            return await next();
        }

        var cached = await db.IdempotencyKeys
            .Where(k => k.BusinessId == businessId && k.ClientRequestId == clientRequestId)
            .Select(k => k.ResponseJson)
            .FirstOrDefaultAsync(cancellationToken);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<TResponse>(cached)!;
        }

        var response = await next();

        db.IdempotencyKeys.Add(new IdempotencyKey
        {
            BusinessId = businessId,
            ClientRequestId = clientRequestId,
            RequestType = typeof(TRequest).Name,
            ResponseJson = JsonSerializer.Serialize(response),
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another replay of the same ClientRequestId won the race and already recorded its
            // own IdempotencyKeys row - this response is still correct to hand back to the
            // caller, it's just not worth persisting a second time. A genuinely different
            // failure (no winner recorded) is not swallowed.
            var winnerExists = await db.IdempotencyKeys
                .AnyAsync(k => k.BusinessId == businessId && k.ClientRequestId == clientRequestId, cancellationToken);
            if (!winnerExists)
            {
                throw;
            }
        }

        return response;
    }
}
