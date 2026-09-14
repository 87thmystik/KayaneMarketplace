namespace Kayane.ViewModels;

public class VendorPayoutVM
{
    public Guid PayoutId { get; set; }
    public Guid VendorId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
}