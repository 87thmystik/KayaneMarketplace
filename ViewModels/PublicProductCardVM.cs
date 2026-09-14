using Kayane.Models;

namespace Kayane.ViewModels
{
    public class PublicProductCardVM
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
    }
}
