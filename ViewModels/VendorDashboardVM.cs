using System.Linq;
using Kayane.Models;

namespace Kayane.ViewModels;

public class  VendorDashboardVM
{
    public string BusinessName { get; set; } = string.Empty;
    public VendorStatus Status { get; set; } // Must be VendorStatus, not object
    public decimal TotalSales { get; set; }
    public int TotalOrders { get; set; }
    public int TotalProducts { get; set; }
    public int PendingProductsCount { get; set; }
    public Vendor Vendor { get; set; }
    public VendorWallet Wallet { get; set; } = new();
    public int PendingProducts { get; set; }
    public decimal TotalEarnings { get; set; }
    public IEnumerable<PayoutTransaction> RecentTransactions { get; set; } = new List<PayoutTransaction>();
    public List<VendorProductListItemVM> RecentProducts { get; set; } = new();

    //how many order items are still awaiting fulfillment
    public int PendingOrdersCount { get; set; }

}