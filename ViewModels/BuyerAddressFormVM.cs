using System.ComponentModel.DataAnnotations;

namespace Kayane.ViewModels;

public class BuyerAddressFormVM
{
    public Guid? AddressId { get; set; }

    [Required, StringLength(50)]
    public string Label { get; set; } = "Home";

    [Required, StringLength(200)]
    public string RecipientName { get; set; } = string.Empty;

    [Required, Phone, StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string AddressLine1 { get; set; } = string.Empty;

    [StringLength(300)]
    public string? AddressLine2 { get; set; }

    [Required, StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string State { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
}