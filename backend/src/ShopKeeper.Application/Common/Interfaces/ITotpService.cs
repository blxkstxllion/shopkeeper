namespace ShopKeeper.Application.Common.Interfaces;

public interface ITotpService
{
    /// <summary>A fresh random Base32-encoded secret, suitable for both server storage and QR provisioning.</summary>
    string GenerateSecret();

    /// <summary>An otpauth:// URI an authenticator app can scan (as a QR code) to add this account.</summary>
    string BuildProvisioningUri(string secret, string accountEmail);

    /// <summary>True if `code` is a valid current (or adjacent-window) TOTP code for `secret`.</summary>
    bool ValidateCode(string secret, string code);
}
