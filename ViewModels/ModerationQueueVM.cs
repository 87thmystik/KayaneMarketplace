using Kayane.Models;

namespace Kayane.ViewModels
{
    public class ModerationQueueVM
    {
        public List<AdminProductListItemVM> Products { get; set; } = new();
        public ProductStatus CurrentFilter { get; set; } = ProductStatus.Pending;
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
    }
}
