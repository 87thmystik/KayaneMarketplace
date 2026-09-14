namespace Kayane.ViewModels;

public class OrderPaymentVM
{
    public string PaymentMethod { get; set; } = string.Empty;
    public string TransactionReference { get; set; } = string.Empty;
    public decimal AmountPaid { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime PaidAt { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
}