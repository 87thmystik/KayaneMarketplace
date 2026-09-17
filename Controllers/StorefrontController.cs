using Kayane.Data;
using Kayane.Models;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kayane.Controllers;

public class StorefrontController : Controller
{
    private readonly KayaneDb _context;

    public StorefrontController(KayaneDb context)
    {
        _context = context;
    }

    // GET: /Storefront/Details/{id}
    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Vendor)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProductId == id
                                      && p.Status == ProductStatus.Approved);

        if (product == null || product.Vendor == null) return NotFound();

        var reviews = await _context.ProductReviews
            .AsNoTracking()
            .Where(r => r.ProductId == id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        double avgRating = reviews.Count > 0 ? reviews.Average(r => (double)r.Rating) : 0.0;

        bool canReview = false;
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            var hasPurchased = await _context.OrderItems
                .AsNoTracking()
                .Include(oi => oi.Order)
                .AnyAsync(oi => oi.ProductId == id
                                && oi.Order.UserId == userId
                                && oi.Order.PaymentStatus == PaymentStatus.Success);

            var alreadyReviewed = await _context.ProductReviews
                .AsNoTracking()
                .AnyAsync(r => r.ProductId == id && r.UserId == userId);

            canReview = hasPurchased && !alreadyReviewed;
        }

        var vm = new ProductDetailVM
        {
            ProductId = product.ProductId,
            Name = product.Name,
            Description = product.Description ?? string.Empty,
            Price = product.Price,
            Stock = product.Stock,
            ImageUrl = product.ImageUrl ?? string.Empty,
            VendorId = product.Vendor.VendorId,
            VendorName = product.Vendor.BusinessName,
            VendorSlug = product.Vendor.Slug,
            AverageRating = avgRating,
            TotalReviewsCount = reviews.Count,
            Reviews = reviews,
            CanUserReview = canReview
        };

        return View(vm);
    }

    // GET: /store/{slug}
    [HttpGet("store/{slug}")]
    public async Task<IActionResult> VendorStore(string slug, string? category = null)
    {
        if (string.IsNullOrWhiteSpace(slug)) return NotFound();

        var vendor = await _context.Vendors
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Slug == slug.ToLower());

        if (vendor == null || vendor.Status != VendorStatus.Active) return NotFound();

        // Approved products for this vendor.
        var baseQuery = _context.Products
            .AsNoTracking()
            .Where(p => p.VendorId == vendor.VendorId
                        && p.Status == ProductStatus.Approved);

        // Categories available in this store.
        var availableCategories = await baseQuery
            .Where(p => p.Category != null)
            .Select(p => p.Category!.Name)
            .Distinct()
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(category))
        {
            baseQuery = baseQuery.Where(p => p.Category != null && p.Category.Name == category);
        }

        // Project the products first.
        var raw = await baseQuery
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.ProductId,
                p.Name,
                p.Price,
                p.ImageUrl,
                CategoryName = p.Category != null ? p.Category.Name : "Uncategorized"
            })
            .ToListAsync();

        // Compute review stats in a second pass (avoids correlated subqueries in the projection).
        var productIds = raw.Select(p => p.ProductId).ToList();

        var stats = await _context.ProductReviews
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

        var statsDict = stats.ToDictionary(
            s => s.ProductId,
            s => (s.AverageRating, s.ReviewCount));

        var products = raw.Select(p =>
        {
            var (avg, count) = statsDict.TryGetValue(p.ProductId, out var st)
                ? st : (0.0, 0);

            return new PublicProductCardVM
            {
                ProductId = p.ProductId,
                Name = p.Name,
                CategoryName = p.CategoryName,
                Price = p.Price,
                ImageUrl = p.ImageUrl,
                AverageRating = avg,
                ReviewCount = count
            };
        }).ToList();

        // Aggregate stats for the whole storefront.
        double storeAvg = stats.Count > 0
            ? stats.Average(s => s.AverageRating)
            : 0.0;
        int storeReviewCount = stats.Sum(s => s.ReviewCount);

        var totalApprovedCount = await _context.Products
            .AsNoTracking()
            .CountAsync(p => p.VendorId == vendor.VendorId
                             && p.Status == ProductStatus.Approved);

        var vm = new VendorStorefrontVM
        {
            VendorId = vendor.VendorId,
            BusinessName = vendor.BusinessName,
            Slug = vendor.Slug,
            Description = vendor.BusinessDescription,
            LogoUrl = string.IsNullOrWhiteSpace(vendor.LogoUrl)
                ? "/images/default-vendor-logo.png"
                : vendor.LogoUrl,
            BannerUrl = string.IsNullOrWhiteSpace(vendor.BannerUrl)
                ? "/images/default-vendor-banner.jpg"
                : vendor.BannerUrl,
            SupportEmail = vendor.SupportEmail,
            SupportPhone = vendor.SupportPhone,
            BusinessPhone = vendor.BusinessPhone ?? string.Empty,
            BusinessAddress = vendor.BusinessAddress ?? string.Empty,
            MemberSince = vendor.CreatedAt,
            AverageRating = storeAvg,
            TotalReviews = storeReviewCount,
            TotalProductsCount = totalApprovedCount,
            Products = products,
            SelectedCategory = category,
            AvailableCategories = availableCategories
        };

        return View(vm);
    }
}