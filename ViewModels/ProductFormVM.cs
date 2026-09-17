using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Kayane.ViewModels
{
    public class ProductFormVM
    {
        public Guid? ProductId { get; set; }

        [Required, StringLength(255)]
        public string Name { get; set; } = string.Empty;

        [DataType(DataType.MultilineText)]
        public string? Description { get; set; }

        [Required, Range(0.01, 1000000.00, ErrorMessage = "Price must be greater than zero.")]
        public decimal Price { get; set; }

        [Required, Range(0, 100000, ErrorMessage = "Stock cannot be negative.")]
        public int Stock { get; set; }

        [Display(Name = "Category")]
        public Guid? CategoryId { get; set; }

        public List<SelectListItem> Categories { get; set; } = new();

        public string? ImageUrl { get; set; }
        public IFormFile? ImageFile { get; set; }
    }
}