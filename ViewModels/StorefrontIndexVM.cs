using Kayane.Models;

namespace Kayane.ViewModels
{
    public class StorefrontIndexVM
    {
        public List<StorefrontProductVM> Products { get; set; } = new();
        public List<Category> Categories { get; set; } = new();
        public Guid? SelectedCategory { get; set; }
        public string? SearchString { get; set; }
    }
}
