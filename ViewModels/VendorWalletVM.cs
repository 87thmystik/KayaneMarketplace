using Kayane.Models;

namespace Kayane.ViewModels;

public class VendorWalletVM
{
    public decimal Balance { get; set; }
    public decimal TotalEarnings { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public List<PayoutTransaction> PayoutHistory { get; set; } = new();
    public RequestPayoutInputModel RequestModel { get; set; } = new();
}

public class RequestPayoutInputModel
{
    public decimal Amount { get; set; }
}