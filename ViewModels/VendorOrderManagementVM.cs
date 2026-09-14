using Kayane.Models;

namespace Kayane.ViewModels
{
    public class VendorOrderManagementVM
    {
        public List<VendorOrderItemVM> OrderItems { get; set; } = new();
        public OrderItemStatus? CurrentFilter { get; set; }
        public int PendingCount { get; set; }
        public int ProcessingCount { get; set; }
        public int ShippedCount { get; set; }
        public int DeliveredCount { get; set; }
        public int CancelledCount { get; set; }
    }
}
