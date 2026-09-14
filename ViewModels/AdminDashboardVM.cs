using Kayane.Models;

namespace Kayane.ViewModels
{
    public class AdminDashboardVM
    {
        public int PendingVendorsCount { get; set; }
        public int ActiveVendorsCount { get; set; }
        public int PendingProductsCount { get; set; }
        public int TotalUsersCount { get; set; }
        public int TotalUsers { get; set; }
        public int TotalVendors { get; set; }
        public int TotalProducts { get; set; }
        public decimal TotalPlatformRevenue { get; set; }
        public int TotalOrdersCount { get; set; }
        public decimal TotalGmv { get; set; }
        public decimal TotalCommissions { get; set; }
        public int PendingPayoutsCount { get; set; }
        public List<AdminRecentOrderVM> RecentOrders { get; set; } = new();
        public List<AdminPendingVendorVM> PendingVendors { get; set; } = new();

        public List<AdminActionLogVM> RecentActions { get; set; } = new();
        public List<AdminProductListItemVM> PendingProducts { get; set; } = new();
    }

}
