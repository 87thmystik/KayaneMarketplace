namespace Kayane.ViewModels
{
    public class ShopProductCardVM
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public string VendorName { get; set; } = "Kayane Partner";
        public string CategoryName { get; set; } = "Uncategorized";

        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public string? ThumbnailUrl { get; set; }

        public bool IsNewArrival { get; set; }   // last 14 days
        public bool OutOfStock { get; set; }
    }
}