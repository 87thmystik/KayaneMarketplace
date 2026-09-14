using Kayane.Models;
using System.ComponentModel.DataAnnotations;

namespace Kayane.ViewModels;

public class CheckoutVM
{
    [Required(ErrorMessage = "Full name is required.")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Invalid email address.")]
    public string CustomerEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    public string CustomerPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Shipping address is required.")]
    public string ShippingAddress { get; set; } = string.Empty;


    [Required(ErrorMessage = "Please select a payment method.")]
    public PaymentMethod SelectedPaymentMethod { get; set; }

    public CartVM Cart { get; set; } = new();
}