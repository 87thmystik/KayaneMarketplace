namespace Kayane.ViewModels
{
    public class VendorAnalyticsVM
    {
        public decimal TotalRevenue { get; set; }
        public decimal CurrentMonthRevenue { get; set; }
        public int TotalOrdersFulfilled { get; set; }
        public int PendingOrdersCount { get; set; }
        public List<TopProductMetricVM> TopProducts { get; set; } = new();
    }
}
