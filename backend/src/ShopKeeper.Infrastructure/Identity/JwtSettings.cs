namespace ShopKeeper.Infrastructure.Identity;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;

    /// <summary>Base64 or plain secret, minimum 32 bytes when UTF8-encoded. Set via environment/user-secrets - never commit a real value.</summary>
    public string Secret { get; set; } = default!;
}
