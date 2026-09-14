using Kayane.Models;

namespace Kayane.ViewModels
{
    public class ProductModerationVM
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public ProductStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }
}
