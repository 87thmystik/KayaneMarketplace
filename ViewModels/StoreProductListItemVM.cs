namespace Kayane.ViewModels
{
    public class StoreProductListItemVM
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;
        public Guid VendorId { get; set; }
    }
}
