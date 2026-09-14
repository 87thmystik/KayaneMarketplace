using Kayane.Data;
using Kayane.Models;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using System.Text.Json;

namespace Kayane.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly KayaneDb _context;
    private readonly IMemoryCache _cache;

    public DashboardController(KayaneDb context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    // GET: /Admin/Dashboard
    [HttpGet]
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var pendingVendors = await _context.Vendors
            .Include(v => v.User)
            .Where(v => v.Status == VendorStatus.Pending)
            .OrderByDescending(v => v.CreatedAt)
            .Take(5)
            .Select(v => new AdminPendingVendorVM
            {
                VendorId = v.VendorId,
                BusinessName = v.BusinessName,
                BusinessAddress = v.BusinessAddress ?? "",
                BusinessEmail = v.SupportEmail ?? v.User.Email,
                BusinessPhone = v.SupportPhone ?? "",
                CreatedAt = v.CreatedAt
            })
            .ToListAsync();

        var pendingProducts = await _context.Products
            .Include(p => p.Vendor)
            .Include(p => p.Category)
            .Where(p => p.Status == ProductStatus.Pending)
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .Select(p => new AdminProductListItemVM
            {
                ProductId = p.ProductId,
                Name = p.Name,
                VendorBusinessName = p.Vendor != null ? p.Vendor.BusinessName : "Unknown",
                CategoryName = p.Category != null ? p.Category.Name : "Uncategorized",
                Price = p.Price,
                Stock = p.Stock,
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                Description = p.Description
            })
            .ToListAsync();

        var model = new AdminDashboardVM
        {
            PendingVendorsCount = await _context.Vendors.CountAsync(v => v.Status == VendorStatus.Pending),
            ActiveVendorsCount = await _context.Vendors.CountAsync(v => v.Status == VendorStatus.Active),
            PendingProductsCount = await _context.Products.CountAsync(p => p.Status == ProductStatus.Pending),
            TotalUsersCount = await _context.Users.CountAsync(),
            TotalUsers = await _context.Users.CountAsync(),
            TotalVendors = await _context.Vendors.CountAsync(),
            TotalProducts = await _context.Products.CountAsync(),
            TotalOrdersCount = await _context.Orders.CountAsync(),
            TotalPlatformRevenue = await _context.Orders
                .Where(o => o.PaymentStatus == PaymentStatus.Success)
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m,
            PendingVendors = pendingVendors,
            PendingProducts = pendingProducts,
            RecentActions = await _context.AdminActions
                .OrderByDescending(a => a.Timestamp)
                .Take(5)
                .Select(a => new AdminActionLogVM
                {
                    ActionType = a.ActionType,
                    TargetType = a.TargetType,
                    TargetId = a.TargetId,
                    Timestamp = a.Timestamp
                })
                .ToListAsync()
        };

        return View(model);
    }

    // GET: /Admin/Vendors
    [HttpGet]
    public async Task<IActionResult> Vendors(VendorStatus? statusFilter, string? searchKeyword, int page = 1)
    {
        int pageSize = 10;
        page = Math.Max(1, page);

        var query = _context.Vendors.AsNoTracking();

        // 1. Status Filter
        if (statusFilter.HasValue)
        {
            query = query.Where(v => v.Status == statusFilter.Value);
        }

        // 2. Search Keyword Filter
        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            var term = searchKeyword.Trim();
            query = query.Where(v =>
                v.BusinessName.Contains(term) ||
                v.User.Name.Contains(term) ||
                v.User.Email.Contains(term));
        }

        // 3. Count Total Items
        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        // 4. Paginate & Project
        var vendors = await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new AdminVendorListItemVM
            {
                VendorId = v.VendorId,
                BusinessName = v.BusinessName,
                BusinessAddress = v.BusinessAddress ?? "",
                OwnerName = v.User == null ? "" : v.User.Name,
                OwnerEmail = v.User == null ? "" : v.User.Email,
                Status = v.Status,
                CreatedAt = v.CreatedAt
            })
            .ToListAsync();

        var vm = new AdminVendorListVM
        {
            Vendors = vendors,
            SelectedStatus = statusFilter,
            SearchKeyword = searchKeyword,
            CurrentPage = page,
            TotalPages = totalPages > 0 ? totalPages : 1,
            TotalCount = totalItems
        };

        return View(vm);
    }

    // POST: /Admin/UpdateVendorStatus
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateVendorStatus(Guid vendorId, VendorStatus status, string? returnUrl = null)
    {
        var vendor = await _context.Vendors.FindAsync(vendorId);
        if (vendor == null) return NotFound();

        var oldStatus = vendor.Status;
        vendor.Status = status;

        await LogAdminActionAsync("vendor_status_change", "Vendor", vendor.VendorId, new
        {
            previousStatus = oldStatus.ToString(),
            newStatus = status.ToString()
        });

        await _context.SaveChangesAsync();

        // Evict vendor status from MemoryCache so ApprovedVendorFilter fetches updated data
        _cache.Remove($"vendor_user_{vendor.UserId}");

        TempData["SuccessMessage"] = $"Vendor status updated to {status}.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Vendors));
    }

    // GET: /Admin/Products
    [HttpGet]
    public async Task<IActionResult> Products(ProductStatus? statusFilter = ProductStatus.Pending)
    {
        var query = _context.Products.Include(p => p.Vendor).AsQueryable();

        if (statusFilter.HasValue)
            query = query.Where(p => p.Status == statusFilter.Value);

        var products = await query.OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProductModerationVM
            {
                ProductId = p.ProductId,
                Name = p.Name,
                Description = p.Description ?? string.Empty,
                Price = p.Price,
                Stock = p.Stock,
                BusinessName = p.Vendor != null ? p.Vendor.BusinessName : "Unknown Vendor",
                Status = p.Status,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        ViewData["SelectedStatus"] = statusFilter;
        return View(products);
    }

    // POST: /Admin/UpdateProductStatus
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProductStatus(Guid productId, ProductStatus status, string? returnUrl = null)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null) return NotFound();

        var oldStatus = product.Status;
        product.Status = status;

        await LogAdminActionAsync("product_status_change", "Product", product.ProductId, new
        {
            previousStatus = oldStatus.ToString(),
            newStatus = status.ToString()
        });

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Product '{product.Name}' status updated to {status}.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Products));
    }

    // GET: /Admin/PendingProducts
    [HttpGet]
    public async Task<IActionResult> PendingProducts()
    {
        var pendingProducts = await _context.Products
            .Include(p => p.Vendor)
            .Include(p => p.Category)
            .Where(p => p.Status == ProductStatus.Pending)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return View(pendingProducts);
    }

    // POST: /Admin/ApproveProduct/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveProduct(Guid id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound();

        product.Status = ProductStatus.Approved;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Product '{product.Name}' has been approved.";
        return RedirectToAction(nameof(PendingProducts));
    }

    // POST: /Admin/RejectProduct/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectProduct(Guid id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound();

        product.Status = ProductStatus.Rejected;
        await _context.SaveChangesAsync();

        TempData["ErrorMessage"] = $"Product '{product.Name}' has been rejected.";
        return RedirectToAction(nameof(PendingProducts));
    }

    #region Private Helpers

    private async Task LogAdminActionAsync(string actionType, string targetType, Guid targetId, object details)
    {
        var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(adminIdClaim, out var adminId))
        {
            var auditLog = new AdminAction
            {
                AdminId = adminId,
                ActionType = actionType,
                TargetType = targetType,
                TargetId = targetId,
                Details = JsonSerializer.Serialize(details),
                Timestamp = DateTime.UtcNow
            };
            _context.Set<AdminAction>().Add(auditLog);
        }
    }

    #endregion
}