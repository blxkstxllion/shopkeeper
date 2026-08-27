namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

/// <summary>
/// A person who can authenticate into the platform. A single User may belong to
/// multiple businesses via BusinessUser (e.g. an accountant who consults for
/// several shops), so User itself is NOT tenant-scoped.
/// </summary>
public class User : BaseEntity
{
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string? PhotoUrl { get; set; }

    public bool IsEmailVerified { get; set; }
    public string? EmailVerificationToken { get; set; }
    public DateTimeOffset? EmailVerificationExpiresAt { get; set; }

    public string? PasswordResetToken { get; set; }
    public DateTimeOffset? PasswordResetExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastLoginAt { get; set; }

    public bool TwoFactorEnabled { get; set; }

    /// <summary>Base32 TOTP secret. Set as soon as setup starts (even before confirmation) - an
    /// unconfirmed secret is harmless since TwoFactorEnabled stays false until /2fa/enable
    /// verifies a real code against it. Overwritten if setup is restarted or abandoned.</summary>
    public string? TwoFactorSecret { get; set; }

    /// <summary>JSON array of bcrypt-hashed one-time recovery codes (never stored in plaintext) -
    /// each is removed from this list the moment it's redeemed.</summary>
    public string? TwoFactorRecoveryCodesJson { get; set; }

    public ICollection<BusinessUser> BusinessUsers { get; set; } = new List<BusinessUser>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
