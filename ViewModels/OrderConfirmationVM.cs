using Kayane.ViewModels;

namespace Kayane.Models // or Kayane.ViewModels
{
    public class OrderConfirmationVM
    {
        public Guid OrderId { get; set; }
        public string? CustomerName { get; set; }
        public string? ShippingAddress { get; set; }
        public string? PaymentStatus { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public OrderItemStatus Status { get; set; }
        public OrderPaymentVM Payment { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}