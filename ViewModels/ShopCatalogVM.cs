namespace Kayane.ViewModels
{
    public class ShopCatalogVM
    {
        public List<ShopProductCardVM> Products { get; set; } = new();
        public List<CategoryFilterVM> Categories { get; set; } = new();

        public Guid? SelectedCategoryId { get; set; }
        public string? SearchQuery { get; set; }

        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 12;
        public int TotalProducts { get; set; }
        public int TotalPages => PageSize > 0
            ? (int)Math.Ceiling(TotalProducts / (double)PageSize)
            : 1;
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

    public class CategoryFilterVM
    {
        public Guid CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}