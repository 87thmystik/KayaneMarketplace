using Kayane.Models;

namespace Kayane.ViewModels
{
    public class ProductDetailVM
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string ImageUrl { get; set; } = string.Empty;

        public Guid VendorId { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public string VendorSlug { get; set; } = string.Empty;   // NEW

        public double AverageRating { get; set; }
        public int TotalReviewsCount { get; set; }
        public List<ProductReview> Reviews { get; set; } = new();
        public bool CanUserReview { get; set; }
    }
}