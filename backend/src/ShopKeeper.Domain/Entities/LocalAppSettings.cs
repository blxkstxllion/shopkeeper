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

    /// <summary>Consecutive wrong PINs since the last success - reset to 0 on success. Only a
    /// 4-digit PIN (10,000 combinations), so without this an unattended app left open could be
    /// unlocked by brute force in well under a minute. Same pattern as User.FailedLoginAttempts.</summary>
    public int FailedUnlockAttempts { get; set; }

    /// <summary>Set once FailedUnlockAttempts crosses the threshold - unlock is rejected outright
    /// (without even checking the PIN) while this is in the future.</summary>
    public DateTimeOffset? UnlockLockedUntil { get; set; }
}
