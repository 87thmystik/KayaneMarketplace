using Kayane.Models;

namespace Kayane.ViewModels;

public class CustomerOrderSummaryVM
{
    public Guid OrderId { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public int ItemCount { get; set; }
}

