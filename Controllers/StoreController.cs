using Kayane.Data;
using Kayane.Models;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kayane.Controllers;

public class StoreController : Controller
{
    private readonly KayaneDb _context;

    public StoreController(KayaneDb context)
    {
        _context = context;
    }

    // GET: /store/{slug}
    [HttpGet("store/{slug}")]
    public async Task<IActionResult> VendorStore(string slug, string? category = null)
    {
        if (string.IsNullOrWhiteSpace(slug)) return NotFound();

        var vendor = await _context.Vendors
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Slug == slug.ToLower());

        if (vendor == null) return NotFound();

        // Fetch vendor's approved products only
        var approvedProductsQuery = _context.Products
            .AsNoTracking()
            .Where(p => p.VendorId == vendor.VendorId && p.Status == ProductStatus.Approved);

        // Get available category list for filtering sidebar
        var availableCategories = await approvedProductsQuery
            .Select(p => p.Category != null ? p.Category.Name : "Uncategorized")
            .Distinct()
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(category))
        {
            approvedProductsQuery = approvedProductsQuery
                .Where(p => p.Category != null && p.Category.Name == category);
        }

        var products = await approvedProductsQuery
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PublicProductCardVM
            {
                ProductId = p.ProductId,
                Name = p.Name,
                CategoryName = p.Category != null ? p.Category.Name : "Uncategorized",
                Price = p.Price,
                ImageUrl = p.ImageUrl,
                AverageRating = 4.8, // Stubbed or calculate from Reviews table if present
                ReviewCount = 12
            })
            .ToListAsync();

        var totalApprovedCount = await _context.Products
            .AsNoTracking()
            .CountAsync(p => p.VendorId == vendor.VendorId && p.Status == ProductStatus.Approved);

        var viewModel = new VendorStorefrontVM
        {
            VendorId = vendor.VendorId,
            BusinessName = vendor.BusinessName,
            Slug = vendor.Slug,
            Description = vendor.BusinessDescription,
            LogoUrl = vendor.LogoUrl ?? "/images/default-vendor-logo.png",
            BannerUrl = vendor.BannerUrl ?? "/images/default-vendor-banner.jpg",
            SupportEmail = vendor.SupportEmail,
            SupportPhone = vendor.SupportPhone,
            MemberSince = vendor.CreatedAt,
            AverageRating = 4.8,
            TotalReviews = 34,
            TotalProductsCount = totalApprovedCount,
            Products = products,
            SelectedCategory = category,
            AvailableCategories = availableCategories
        };

        return View(viewModel);
    }
}