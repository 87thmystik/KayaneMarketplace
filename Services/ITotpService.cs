namespace Kayane.Services;

public interface ITotpService
{
    /// <summary>Generates a new base32 secret (160 bits / 32 chars).</summary>
    string GenerateSecret();

    /// <summary>Verifies a 6-digit TOTP code against a secret. Allows ±30s drift.</summary>
    bool VerifyCode(string secret, string code);

    /// <summary>Returns the otpauth:// URI used to generate the QR code.</summary>
    string GetProvisioningUri(string secret, string email, string issuer = "Kayane Admin");

    /// <summary>Generates N recovery codes. Returns the plaintext codes (caller hashes them).</summary>
    List<string> GenerateRecoveryCodes(int count = 10);
}