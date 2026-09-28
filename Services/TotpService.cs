using OtpNet;

namespace Kayane.Services;

public class TotpService : ITotpService
{
    public string GenerateSecret()
    {
        // 20 bytes = 160 bits — standard TOTP secret length
        var bytes = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(bytes);
    }

    public bool VerifyCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
            return false;

        // Normalize: strip spaces, uppercase
        var normalized = code.Trim().Replace(" ", "");

        if (normalized.Length != 6 || !normalized.All(char.IsDigit))
            return false;

        try
        {
            var secretBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(secretBytes);

            // VerifyTotp with a 1-step tolerance (±30 seconds) for clock drift
            var window = new VerificationWindow(previous: 1, future: 1);

            return totp.VerifyTotp(normalized, out long _, window);
        }
        catch
        {
            return false;
        }
    }

    public string GetProvisioningUri(string secret, string email, string issuer = "Kayane Admin")
    {
        // Format: otpauth://totp/Issuer:user@example.com?secret=XXX&issuer=Issuer
        // This is what authenticator apps understand
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedEmail = Uri.EscapeDataString(email);
        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={secret}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period=30";
    }

    public List<string> GenerateRecoveryCodes(int count = 10)
    {
        var codes = new List<string>();

        for (int i = 0; i < count; i++)
        {
            // 8-character alphanumeric codes
            var bytes = KeyGeneration.GenerateRandomKey(6);   // 48 bits
            var code = Base32Encoding.ToString(bytes)
                .Replace("=", "")
                .Replace("O", "0")
                .Replace("I", "1")
                .Substring(0, 8);

            codes.Add(code);
        }

        return codes;
    }
}