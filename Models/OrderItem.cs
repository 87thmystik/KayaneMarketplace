namespace Kayane.Models
{

    public class OrderItem
    {
        public Guid OrderItemId { get; set; }

        public Guid OrderId { get; set; }
        public Order Order { get; set; } = null!;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Price { get; set; }
        public decimal TotalPrice { get; set; }

        public OrderItemStatus Status { get; set; } = OrderItemStatus.Pending;
        public string? ShippingCarrier { get; set; }
        public string? TrackingNumber { get; set; }
    }
}
