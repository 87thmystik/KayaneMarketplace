using Kayane.Models;

namespace Kayane.ViewModels
{
    public class OrderItemVM
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }

        //shipping info
        public OrderItemStatus Status { get; set; }
        public string? ShippingCarrier { get; set; }
        public string? TrackingNumber { get; set; }
    }
}
