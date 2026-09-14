namespace Kayane.ViewModels;

public class CartItemVM
{
    public Guid ProductId { get; set; }
    public Guid VendorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal Total => Price * Quantity;
    public string BusinessName { get; set; } = string.Empty; // Optional: Vendor's business name
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice => Price; // Optional: Unit price of the product
    public decimal TotalPrice => Price * Quantity;
}

