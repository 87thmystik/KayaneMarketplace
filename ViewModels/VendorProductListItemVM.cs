using Kayane.Models;

namespace Kayane.ViewModels;

public class VendorProductListItemVM
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public ProductStatus Status { get; set; } // Must be ProductStatus, not object
    public DateTime CreatedAt { get; set; }
    public string? ImageUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int StockQuantity { get; set; }
}