using System.ComponentModel.DataAnnotations.Schema;


// Models/Product.cs
namespace Kayane.Models;

public class Product
{
    public Guid ProductId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public ProductStatus Status { get; set; } = ProductStatus.Pending;
    public Vendor? Vendor { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? RejectionReason { get; set; }
    public int Stock { get; set; } = 0;
    public Guid VendorId { get; set; }
    public Category? Category { get; set; }
    public Guid? CategoryId { get; set; } = new Guid();
    public string? ImageUrl { get; set; }

    [Column("thumbnail_url")]
    public string? ThumbnailUrl { get; set; }
}