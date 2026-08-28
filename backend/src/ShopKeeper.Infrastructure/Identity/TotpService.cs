namespace ShopKeeper.Infrastructure.Identity;

using System.Web;
using OtpNet;
using ShopKeeper.Application.Common.Interfaces;

public class TotpService : ITotpService
{
    private const string Issuer = "ShopKeeper";

    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    public string BuildProvisioningUri(string secret, string accountEmail)
    {
        var label = HttpUtility.UrlEncode($"{Issuer}:{accountEmail}");
        var issuer = HttpUtility.UrlEncode(Issuer);
        return $"otpauth://totp/{label}?secret={secret}&issuer={issuer}&digits=6&period=30";
    }

    public bool ValidateCode(string secret, string code)
    {
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        // +/-1 step (30s each) tolerates minor clock drift between server and the user's device.
        return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
    }
}
