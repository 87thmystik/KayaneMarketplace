namespace Kayane.ViewModels
{
    public class StoreCatalogVM
    {
        public List<StoreProductListItemVM> Products { get; set; } = new();
        public string? SearchQuery { get; set; }
        public Guid? VendorFilter { get; set; }
    }
}
