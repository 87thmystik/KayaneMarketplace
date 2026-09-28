namespace Kayane.ViewModels;

public class TwoFactorSetupVM
{
    public string Secret { get; set; } = string.Empty;
    public string QrCodeUrl { get; set; } = string.Empty;   // the otpauth:// URI
    public List<string> RecoveryCodes { get; set; } = new();
    public string? VerificationCode { get; set; }
    public bool ShowRecoveryCodes { get; set; }
}