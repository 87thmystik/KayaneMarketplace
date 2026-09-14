using Kayane.Data;
using Kayane.Models;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kayane.Controllers;

public class ShopController : Controller
{
    private readonly KayaneDb _context;

    public ShopController(KayaneDb context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? search, Guid? categoryId)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Vendor)
            .Where(p => p.Status == ProductStatus.Approved && p.Vendor != null && p.Vendor.Status == VendorStatus.Active);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search; // search is guaranteed non-null here from the if condition
            query = query.Where(p => (p.Name ?? "").Contains(searchTerm) || (p.Description ?? "").Contains(searchTerm));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        var model = new ShopCatalogVM
        {
            Products = await query.OrderByDescending(p => p.CreatedAt).ToListAsync(),
            Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync(), // Loads full Category entities
            SelectedCategoryId = categoryId,
            SearchQuery = search
        };

        return View(model);
    }
}