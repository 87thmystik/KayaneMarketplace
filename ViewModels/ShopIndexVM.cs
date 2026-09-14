using Kayane.Models;

namespace Kayane.ViewModels;

public class ShopIndexVM
{
    public List<Product> Products { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public string? Search { get; set; }
    public Guid? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string SortBy { get; set; } = "newest";
}