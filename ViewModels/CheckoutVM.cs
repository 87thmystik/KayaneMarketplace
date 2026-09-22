using Kayane.Models;
using System.ComponentModel.DataAnnotations;

namespace Kayane.ViewModels;

public class CheckoutVM
{
    [Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Invalid email address.")]
    public string CustomerEmail { get; set; } = string.Empty;

    // These become optional — required only when using a new address
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a payment method.")]
    public PaymentMethod SelectedPaymentMethod { get; set; }

    public CartVM Cart { get; set; } = new();

    // Saved-address support
    public List<BuyerAddress> SavedAddresses { get; set; } = new();
    public Guid? SelectedAddressId { get; set; }
    public bool SaveNewAddress { get; set; }
}