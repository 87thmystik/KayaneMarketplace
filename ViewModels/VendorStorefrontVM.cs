namespace Kayane.ViewModels
{
    public class VendorStorefrontVM
    {
        public Guid VendorId { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? BannerUrl { get; set; }
        public string? SupportEmail { get; set; }
        public string BusinessPhone { get; set; } = string.Empty;
        public string BusinessAddress { get; set; } = string.Empty;
        public string? SupportPhone { get; set; }
        public DateTime MemberSince { get; set; }

        // Rating & Analytics Aggregates
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int TotalProductsCount { get; set; }

        // Filtered Products
        public List<PublicProductCardVM> Products { get; set; } = new();
        public string? SelectedCategory { get; set; }
        public List<string> AvailableCategories { get; set; } = new();

    }
}
