using Kayane.Data;
using Kayane.Models;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kayane.Controllers;

public class ShopController : Controller
{
    private readonly KayaneDb _context;
    private const int DefaultPageSize = 12;

    public ShopController(KayaneDb context)
    {
        _context = context;
    }

    // GET: /Shop
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        Guid? categoryId,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 48) pageSize = DefaultPageSize;

        // Base filter: approved products from active vendors only.
        var query = _context.Products
            .AsNoTracking()
            .Where(p => p.Status == ProductStatus.Approved
                        && p.Vendor != null
                        && p.Vendor.Status == VendorStatus.Active);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                p.Name.Contains(term) ||
                (p.Description != null && p.Description.Contains(term)));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        var totalProducts = await query.CountAsync();

        // Page of raw product data.
        var raw = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.ProductId,
                p.Name,
                p.Description,
                p.Price,
                p.ImageUrl,
                p.Stock,
                p.CreatedAt,
                VendorName = p.Vendor != null ? p.Vendor.BusinessName : "Kayane Partner",
                CategoryName = p.Category != null ? p.Category.Name : "Uncategorized"
            })
            .ToListAsync();

        // Second query: rating aggregates for this page of products.
        var productIds = raw.Select(p => p.ProductId).ToList();

        var ratingStats = await _context.ProductReviews
            .AsNoTracking()
            .Where(r => productIds.Contains(r.ProductId))
            .GroupBy(r => r.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                AverageRating = g.Average(r => (double)r.Rating),
                ReviewCount = g.Count()
            })
            .ToListAsync();

        var statsDict = ratingStats.ToDictionary(s => s.ProductId);

        var newCutoff = DateTime.UtcNow.AddDays(-14);

        var products = raw.Select(p =>
        {
            statsDict.TryGetValue(p.ProductId, out var s);
            return new ShopProductCardVM
            {
                ProductId = p.ProductId,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                ImageUrl = p.ImageUrl,
                VendorName = p.VendorName,
                CategoryName = p.CategoryName,
                AverageRating = s?.AverageRating ?? 0,
                ReviewCount = s?.ReviewCount ?? 0,
                IsNewArrival = p.CreatedAt >= newCutoff,
                OutOfStock = p.Stock <= 0
            };
        }).ToList();

        // Sidebar categories: only those with at least one approved product.
        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.Products.Any(p => p.Status == ProductStatus.Approved))
            .OrderBy(c => c.Name)
            .Select(c => new CategoryFilterVM
            {
                CategoryId = c.CategoryId,
                Name = c.Name
            })
            .ToListAsync();

        var vm = new ShopCatalogVM
        {
            Products = products,
            Categories = categories,
            SelectedCategoryId = categoryId,
            SearchQuery = search,
            CurrentPage = page,
            PageSize = pageSize,
            TotalProducts = totalProducts
        };

        return View(vm);
    }
}