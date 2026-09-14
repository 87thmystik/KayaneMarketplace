namespace Kayane.Models;

public class Category
{
    public Guid CategoryId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    // Navigation property
    public ICollection<Product> Products { get; set; } = new List<Product>();
}