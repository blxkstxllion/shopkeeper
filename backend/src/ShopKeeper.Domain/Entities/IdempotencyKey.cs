namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

/// <summary>
/// Backs IdempotencyBehavior (Application/Common/Behaviors) - the generic version of the
/// pattern CreateSaleCommand pioneered for offline-queued sales, extended to every other
/// offline-eligible mutation. One row per successfully-handled request that carried a
/// client-generated ClientRequestId, so a retried sync (after a lost response, not a failed
/// one) returns the original result instead of creating a duplicate.
/// </summary>
public class IdempotencyKey : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Guid ClientRequestId { get; set; }
    public string RequestType { get; set; } = default!;
    public string ResponseJson { get; set; } = default!;
}
