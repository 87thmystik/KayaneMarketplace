using System.ComponentModel.DataAnnotations;

namespace Kayane.Models;

public class ProductReview
{
    [Key]
    public Guid ReviewId { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public Guid UserId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int Rating { get; set; } // 1 to 5
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}