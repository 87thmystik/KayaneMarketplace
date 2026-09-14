using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kayane.Models;

public class Order
{
    [Key]
    public Guid OrderId { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; } = Guid.NewGuid();
    public User User { get; set; } = null!;

    public PaymentStatus PaymentStatus { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public Payment? Payment { get; set; }

    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? ShippingAddress { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
}