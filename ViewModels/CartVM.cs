namespace Kayane.ViewModels
{

    public class CartVM
    {
        public List<CartItemVM> Items { get; set; } = new();
        public decimal GrandTotal => Items.Sum(i => i.Total);
    }

}
