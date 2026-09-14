using Kayane.Models;

namespace Kayane.ViewModels
{
    public class AdminRecentOrderVM
    {
        public Guid OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public OrderStatus Status { get; set; }
    }
}
