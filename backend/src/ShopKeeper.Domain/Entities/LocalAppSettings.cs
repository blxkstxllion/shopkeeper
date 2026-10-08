namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

/// <summary>
/// Offline-edition-only: a single row holding the app-lock PIN, never present in the SaaS
/// deployment's Postgres schema (only ever created/queried by ShopKeeper.Api.Local). Deliberately
/// NOT ITenantEntity - it has to be readable before any Business/BusinessId exists at all (first
/// launch, before setup has run), so it can never participate in the tenant query filter.
/// </summary>
public class LocalAppSettings : BaseEntity
{
    public string StoreName { get; set; } = default!;
    public string PinHash { get; set; } = default!;
    public DateTimeOffset SetupCompletedAt { get; set; }
}
