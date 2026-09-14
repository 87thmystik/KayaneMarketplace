namespace Kayane.ViewModels
{
    public class StorefrontProductVM
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
