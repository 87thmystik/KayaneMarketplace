using System.ComponentModel.DataAnnotations;

namespace Kayane.ViewModels;

public class RegisterVendorVM
{
    [Required(ErrorMessage = "Full name is required.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [Phone(ErrorMessage = "Invalid phone number.")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Business name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Business name must be between 2 and 100 characters.")]
    public string BusinessName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Business description is required.")]
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string BusinessDescription { get; set; } = string.Empty;

    [Required(ErrorMessage = "Business address is required.")]
    public string BusinessAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters long.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty; 
    public string AccountNumber { get; set; } = string.Empty;
}