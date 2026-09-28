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

        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalItems { get; set; }
        public int TotalPages => PageSize > 0
            ? (int)Math.Ceiling(TotalItems / (double)PageSize)
            : 1;
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }
}