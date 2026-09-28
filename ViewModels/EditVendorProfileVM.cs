using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Kayane.ViewModels;

public class EditVendorProfileVM
{
    [Required]
    public Guid VendorId { get; set; }

    [Required(ErrorMessage = "Business name is required.")]
    [StringLength(100)]
    public string BusinessName { get; set; } = string.Empty;

    [StringLength(300)]
    public string? BusinessAddress { get; set; }

    [Phone(ErrorMessage = "Invalid phone number.")]
    [StringLength(30)]
    public string? SupportPhone { get; set; }

    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [StringLength(200)]
    public string? SupportEmail { get; set; }

    [StringLength(100)]
    public string? BankName { get; set; }

    [StringLength(10)]
    public string? BankCode { get; set; }

    [StringLength(20)]
    public string? AccountNumber { get; set; }

    [StringLength(100)]
    public string? AccountName { get; set; }

    public IFormFile? LogoFile { get; set; }
}