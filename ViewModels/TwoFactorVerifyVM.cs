using System.ComponentModel.DataAnnotations;

namespace Kayane.ViewModels;

public class TwoFactorVerifyVM
{
    [Required(ErrorMessage = "Enter the 6-digit code.")]
    [StringLength(10, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
    public bool UseRecoveryCode { get; set; }
}