using Kayane.Data;
using Kayane.Models;
using Kayane.Services;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kayane.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly KayaneDb _context;
    private readonly IMemoryCache _cache;
    private readonly IAdminAuditService _audit;

    public DashboardController(
        KayaneDb context,
        IMemoryCache cache,
        IAdminAuditService audit)
    {
        _context = context;
        _cache = cache;
        _audit = audit;
    }

    // GET: /Admin/Dashboard
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
                CreatedAt = v.CreatedAt,
                LogoUrl = v.LogoUrl
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

    // GET: /Admin/Dashboard/Vendors
    [HttpGet]
    public async Task<IActionResult> Vendors(VendorStatus? statusFilter, string? searchKeyword, int page = 1)
    {
        int pageSize = 10;
        page = Math.Max(1, page);

        var query = _context.Vendors.AsNoTracking();

        if (statusFilter.HasValue)
        {
            query = query.Where(v => v.Status == statusFilter.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            var term = searchKeyword.Trim();
            query = query.Where(v =>
                v.BusinessName.Contains(term) ||
                v.User.Name.Contains(term) ||
                v.User.Email.Contains(term));
        }

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

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
                OwnerPhone = v.User == null ? "" : v.User.Phone,
                Status = v.Status,
                CreatedAt = v.CreatedAt,
                LogoUrl = v.LogoUrl,
                OwnerAvatarUrl = v.User != null ? v.User.AvatarUrl : null
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

    // GET: /Admin/Dashboard/Products
    [HttpGet]
    public async Task<IActionResult> Products(ProductStatus? statusFilter = ProductStatus.Pending)
    {
        var currentStatus = statusFilter ?? ProductStatus.Pending;

        var baseQuery = _context.Products.AsNoTracking();

        var pendingCount = await baseQuery.CountAsync(p => p.Status == ProductStatus.Pending);
        var approvedCount = await baseQuery.CountAsync(p => p.Status == ProductStatus.Approved);
        var rejectedCount = await baseQuery.CountAsync(p => p.Status == ProductStatus.Rejected);

        var products = await baseQuery
            .Where(p => p.Status == currentStatus)
            .Include(p => p.Vendor)
            .Include(p => p.Category)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new AdminProductListItemVM
            {
                ProductId = p.ProductId,
                Name = p.Name,
                Description = p.Description ?? string.Empty,
                Price = p.Price,
                Stock = p.Stock,
                VendorBusinessName = p.Vendor != null ? p.Vendor.BusinessName : "Unknown Vendor",
                CategoryName = p.Category != null ? p.Category.Name : "Uncategorized",
                Status = p.Status,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        var vm = new ModerationQueueVM
        {
            Products = products,
            CurrentFilter = currentStatus,
            PendingCount = pendingCount,
            ApprovedCount = approvedCount,
            RejectedCount = rejectedCount
        };

        return View(vm);
    }

    // POST: /Admin/Dashboard/Approve/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound();

        var oldStatus = product.Status;
        product.Status = ProductStatus.Approved;

        await _audit.LogAsync("product_approved", "Product", product.ProductId, new
        {
            productName = product.Name,
            previousStatus = oldStatus.ToString()
        });

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Product '{product.Name}' approved.";
        return RedirectToAction(nameof(Products), new { statusFilter = ProductStatus.Pending });
    }

    // POST: /Admin/Dashboard/Reject
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid productId, string? rejectionReason)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null) return NotFound();

        var oldStatus = product.Status;
        product.Status = ProductStatus.Rejected;
        product.RejectionReason = rejectionReason;

        await _audit.LogAsync("product_rejected", "Product", product.ProductId, new
        {
            productName = product.Name,
            previousStatus = oldStatus.ToString(),
            reason = rejectionReason
        });

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Product '{product.Name}' rejected.";
        return RedirectToAction(nameof(Products), new { statusFilter = ProductStatus.Pending });
    }

    // POST: /Admin/Dashboard/UpdateProductStatus
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProductStatus(Guid productId, ProductStatus status, string? returnUrl = null)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null) return NotFound();

        var oldStatus = product.Status;
        product.Status = status;

        await _audit.LogAsync("product_status_change", "Product", product.ProductId, new
        {
            previousStatus = oldStatus.ToString(),
            newStatus = status.ToString(),
            productName = product.Name
        });

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Product '{product.Name}' status updated to {status}.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Products));
    }

    // POST: /Admin/Dashboard/UpdateVendorStatus
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateVendorStatus(Guid vendorId, VendorStatus status, string? returnUrl = null)
    {
        var vendor = await _context.Vendors.FindAsync(vendorId);
        if (vendor == null) return NotFound();

        var oldStatus = vendor.Status;
        vendor.Status = status;

        await _audit.LogAsync("vendor_status_change", "Vendor", vendor.VendorId, new
        {
            previousStatus = oldStatus.ToString(),
            newStatus = status.ToString(),
            businessName = vendor.BusinessName
        });

        await _context.SaveChangesAsync();

        _cache.Remove($"vendor_user_{vendor.UserId}");

        TempData["SuccessMessage"] = $"Vendor status updated to {status}.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Vendors));
    }

    // ===================== BULK PRODUCT ACTIONS =====================

    // POST: /Admin/Dashboard/BulkApproveProducts
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkApproveProducts(Guid[] productIds)
    {
        if (productIds == null || productIds.Length == 0)
        {
            TempData["ErrorMessage"] = "No products selected.";
            return RedirectToAction(nameof(Products));
        }

        var products = await _context.Products
            .Where(p => productIds.Contains(p.ProductId))
            .ToListAsync();

        var result = new BulkActionResultVM { Total = productIds.Length };

        foreach (var product in products)
        {
            if (product.Status == ProductStatus.Approved)
            {
                result.Skipped++;
                continue;
            }

            var oldStatus = product.Status;
            product.Status = ProductStatus.Approved;

            await _audit.LogAsync("product_approved", "Product", product.ProductId, new
            {
                productName = product.Name,
                previousStatus = oldStatus.ToString(),
                via = "bulk action"
            });

            result.Succeeded++;
        }

        result.Skipped += productIds.Length - products.Count;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = result.SuccessMessage();
        return RedirectToAction(nameof(Products), new { statusFilter = ProductStatus.Pending });
    }

    // POST: /Admin/Dashboard/BulkRejectProducts
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkRejectProducts(Guid[] productIds, string? rejectionReason)
    {
        if (productIds == null || productIds.Length == 0)
        {
            TempData["ErrorMessage"] = "No products selected.";
            return RedirectToAction(nameof(Products));
        }

        var products = await _context.Products
            .Where(p => productIds.Contains(p.ProductId))
            .ToListAsync();

        var reasonText = string.IsNullOrWhiteSpace(rejectionReason)
            ? "Does not meet listing guidelines."
            : rejectionReason.Trim();

        var result = new BulkActionResultVM { Total = productIds.Length };

        foreach (var product in products)
        {
            if (product.Status == ProductStatus.Rejected)
            {
                result.Skipped++;
                continue;
            }

            var oldStatus = product.Status;
            product.Status = ProductStatus.Rejected;
            product.RejectionReason = reasonText;

            await _audit.LogAsync("product_rejected", "Product", product.ProductId, new
            {
                productName = product.Name,
                previousStatus = oldStatus.ToString(),
                reason = reasonText,
                via = "bulk action"
            });

            result.Succeeded++;
        }

        result.Skipped += productIds.Length - products.Count;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = result.SuccessMessage();
        return RedirectToAction(nameof(Products), new { statusFilter = ProductStatus.Pending });
    }

    // ===================== BULK VENDOR ACTIONS =====================

    // POST: /Admin/Dashboard/BulkApproveVendors
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkApproveVendors(Guid[] vendorIds)
    {
        if (vendorIds == null || vendorIds.Length == 0)
        {
            TempData["ErrorMessage"] = "No vendors selected.";
            return RedirectToAction(nameof(Vendors));
        }

        var vendors = await _context.Vendors
            .Where(v => vendorIds.Contains(v.VendorId))
            .ToListAsync();

        var result = new BulkActionResultVM { Total = vendorIds.Length };
        var evictedKeys = new List<string>();

        foreach (var vendor in vendors)
        {
            if (vendor.Status == VendorStatus.Active)
            {
                result.Skipped++;
                continue;
            }

            var oldStatus = vendor.Status;
            vendor.Status = VendorStatus.Active;

            await _audit.LogAsync("vendor_status_change", "Vendor", vendor.VendorId, new
            {
                previousStatus = oldStatus.ToString(),
                newStatus = VendorStatus.Active.ToString(),
                businessName = vendor.BusinessName,
                via = "bulk action"
            });

            evictedKeys.Add($"vendor_user_{vendor.UserId}");
            result.Succeeded++;
        }

        result.Skipped += vendorIds.Length - vendors.Count;

        await _context.SaveChangesAsync();

        foreach (var key in evictedKeys)
            _cache.Remove(key);

        TempData["SuccessMessage"] = result.SuccessMessage();
        return RedirectToAction(nameof(Vendors), new { statusFilter = VendorStatus.Pending });
    }

    // POST: /Admin/Dashboard/BulkRejectVendors
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkRejectVendors(Guid[] vendorIds)
    {
        if (vendorIds == null || vendorIds.Length == 0)
        {
            TempData["ErrorMessage"] = "No vendors selected.";
            return RedirectToAction(nameof(Vendors));
        }

        var vendors = await _context.Vendors
            .Where(v => vendorIds.Contains(v.VendorId))
            .ToListAsync();

        var result = new BulkActionResultVM { Total = vendorIds.Length };

        foreach (var vendor in vendors)
        {
            if (vendor.Status == VendorStatus.Rejected)
            {
                result.Skipped++;
                continue;
            }

            var oldStatus = vendor.Status;
            vendor.Status = VendorStatus.Rejected;

            await _audit.LogAsync("vendor_status_change", "Vendor", vendor.VendorId, new
            {
                previousStatus = oldStatus.ToString(),
                newStatus = VendorStatus.Rejected.ToString(),
                businessName = vendor.BusinessName,
                via = "bulk action"
            });

            result.Succeeded++;
        }

        result.Skipped += vendorIds.Length - vendors.Count;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = result.SuccessMessage();
        return RedirectToAction(nameof(Vendors), new { statusFilter = VendorStatus.Pending });
    }

    // GET: /Admin/Dashboard/PendingProducts
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

    // POST: /Admin/Dashboard/ApproveProduct/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveProduct(Guid id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound();

        product.Status = ProductStatus.Approved;

        await _audit.LogAsync("product_approved", "Product", product.ProductId, new
        {
            productName = product.Name
        });

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Product '{product.Name}' has been approved.";
        return RedirectToAction(nameof(PendingProducts));
    }

    // POST: /Admin/Dashboard/RejectProduct/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectProduct(Guid id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound();

        product.Status = ProductStatus.Rejected;

        await _audit.LogAsync("product_rejected", "Product", product.ProductId, new
        {
            productName = product.Name
        });

        await _context.SaveChangesAsync();

        TempData["ErrorMessage"] = $"Product '{product.Name}' has been rejected.";
        return RedirectToAction(nameof(PendingProducts));
    }

    // GET: /Admin/Dashboard/Approvals
    [HttpGet]
    public async Task<IActionResult> Approvals()
    {
        ViewBag.PendingPayoutsCount = await _context.PayoutTransactions.CountAsync(p => p.Status == "Pending");
        ViewBag.PendingVendorsCount = await _context.Vendors.CountAsync(v => v.Status == VendorStatus.Pending);
        ViewBag.PendingProductsCount = await _context.Products.CountAsync(p => p.Status == ProductStatus.Pending);
        ViewBag.TotalOrders = await _context.Orders.CountAsync();
        return View();
    }
}