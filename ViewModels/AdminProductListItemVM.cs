using Kayane.Models;

namespace Kayane.ViewModels
{
    public class AdminProductListItemVM
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string VendorBusinessName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Description { get; set; }
        public ProductStatus Status { get; set; }
    }
}
