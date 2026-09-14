using Kayane.Models;

namespace Kayane.ViewModels;

public class ShopCatalogVM
{
    // Must be List<Product>, not string or List<string>
    public List<Product> Products { get; set; } = new();

    // Must be List<Category>, not string or List<string>
    public List<Category> Categories { get; set; } = new();

    public Guid? SelectedCategoryId { get; set; }
    public string? SearchQuery { get; set; }
}