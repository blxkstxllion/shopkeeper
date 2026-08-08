namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

/// <summary>
/// Refresh tokens are stored hashed (never in plaintext) and rotated on every use:
/// redeeming a token issues a new one and marks this one as replaced, so a stolen
/// token that gets reused after rotation is detectable and revocable.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    /// <summary>The tenant this token's access-token claims were scoped to at issuance, so rotation preserves context.</summary>
    public Guid? ActiveBusinessId { get; set; }

    public string TokenHash { get; set; } = default!;
    public DateTimeOffset ExpiresAt { get; set; }

    public string? CreatedByIp { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokedByIp { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public string? ReasonRevoked { get; set; }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;
    public bool IsActive => !IsRevoked && !IsExpired;
}
