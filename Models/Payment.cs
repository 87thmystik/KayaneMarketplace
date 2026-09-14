using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kayane.Models;

public class Payment
{
    [Key]
    public Guid PaymentId { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public string PaymentProvider { get; set; } = "Paystack";
    public string? PaymentReference { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}