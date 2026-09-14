using Kayane.Models;

namespace Kayane.ViewModels
{
    public class VendorOrderItemVM
    {
        public Guid OrderItemId { get; set; }
        public Guid OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;

        // Ensure this is the enum type, NOT a string
        public OrderItemStatus Status { get; set; }

        public string? ShippingCarrier { get; set; }
        public string? TrackingNumber { get; set; }
    }
}