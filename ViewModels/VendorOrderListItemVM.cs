using Kayane.Models;

namespace Kayane.ViewModels;

public class VendorOrderListItemVM
{
    public Guid OrderItemId { get; set; }
    public Guid OrderId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
}