using System.ComponentModel.DataAnnotations;

namespace Kayane.ViewModels
{
    public class ForgotPasswordVM
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}