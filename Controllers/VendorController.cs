using Kayane.Data;
using Kayane.Extensions;
using Kayane.Filters;
using Kayane.Models;
using Kayane.Services;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kayane.Controllers;

[Authorize]
public class VendorController : Controller
{
    private readonly KayaneDb _context;
    private readonly IEmailService _emailService;
    private readonly IWebHostEnvironment _environment;

    public VendorController(
        KayaneDb context,
        IEmailService emailService,
        IWebHostEnvironment environment)
    {
        _context = context;
        _emailService = emailService;
        _environment = environment;
    }

    // Helper property to retrieve the Vendor injected by [ApprovedVendor] filter
    private Vendor CurrentVendor => HttpContext.GetCurrentVendor();

    #region Private Helper Methods

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private async Task<Vendor?> GetCurrentVendorAsync()
    {
        if (CurrentUserId is not { } userId) return null;

        return await _context.Vendors
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.UserId == userId);
    }

    private async Task<List<SelectListItem>> GetCategorySelectListAsync(Guid? selectedCategoryId = null)
    {
        return await _context.Categories
            .AsNoTracking()
            .Select(c => new SelectListItem
            {
                Value = c.CategoryId.ToString(),
                Text = c.Name,
                Selected = selectedCategoryId.HasValue && c.CategoryId == selectedCategoryId.Value
            })
            .ToListAsync();
    }

    private async Task<(string? RelativePath, string? ErrorMessage)> ProcessImageUploadAsync(
        IFormFile? file,
        string subFolder)
    {
        if (file == null || file.Length == 0) return (null, null);

        const long maxBytes = 5 * 1024 * 1024;   // 5 MB
        if (file.Length > maxBytes)
            return (null, "Image must be smaller than 5 MB.");

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            return (null, "Only .jpg, .jpeg, .png, and .webp images are allowed.");

        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", subFolder);
        Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return ($"/uploads/{subFolder}/{uniqueFileName}", null);
    }

    private void DeleteImageIfExists(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;

        // Only delete files under /uploads/ to avoid touching seeded/static assets.
        if (!relativePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)) return;

        var fullPath = Path.Combine(
            _environment.WebRootPath,
            relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        if (System.IO.File.Exists(fullPath))
        {
            try { System.IO.File.Delete(fullPath); }
            catch { /* best-effort — don't fail the request */ }
        }
    }

    #endregion

    #region Registration & Status Flow (Unfiltered)

    // GET: /Vendor/ApplicationStatus
    [HttpGet]
    public async Task<IActionResult> ApplicationStatus()
    {
        var vendor = await GetCurrentVendorAsync();
        if (vendor == null)
        {
            return RedirectToAction("RegisterVendor", "Auth");
        }

        return View(vendor);
    }

    #endregion

    #region Approved Vendor Management

    // GET: /Vendor/Dashboard
    [HttpGet]
    [ApprovedVendor]
    public async Task<IActionResult> Dashboard()
    {
        var vendorId = CurrentVendor.VendorId;

        var vendor = await _context.Vendors
            .Include(v => v.Wallet)
            .Include(v => v.Products)
            .FirstOrDefaultAsync(v => v.VendorId == vendorId);

        if (vendor == null)
        {
            return NotFound();
        }

        var wallet = vendor.Wallet ?? new VendorWallet { Balance = 0.00m, TotalEarned = 0.00m };

        var recentTransactions = await _context.PayoutTransactions
            .Where(p => p.VendorId == vendorId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .ToListAsync();

        var pendingOrdersCount = await _context.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Product.VendorId == vendorId
                         && oi.Status == OrderItemStatus.Pending)
            .CountAsync();

        var viewModel = new VendorDashboardVM
        {
            Vendor = vendor,
            Wallet = wallet,
            TotalProducts = vendor.Products?.Count ?? 0,
            PendingProducts = vendor.Products?.Count(p => p.Status == ProductStatus.Pending) ?? 0,
            PendingOrdersCount = pendingOrdersCount,
            TotalEarnings = wallet.TotalEarned,
            RecentTransactions = recentTransactions
        };

        return View(viewModel);
    }

    // GET: /Vendor/Products
    [HttpGet]
    [ApprovedVendor]
    public async Task<IActionResult> Products()
    {
        var products = await _context.Products
            .AsNoTracking()
            .Where(p => p.VendorId == CurrentVendor.VendorId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new VendorProductListItemVM
            {
                ProductId = p.ProductId,
                Name = p.Name,
                Price = p.Price,
                Stock = p.Stock,
                CategoryName = p.Category != null ? p.Category.Name : "Uncategorized",
                Status = p.Status,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        return View(products);
    }

    // GET: /Vendor/CreateProduct
    [HttpGet]
    [ApprovedVendor]
    public async Task<IActionResult> CreateProduct()
    {
        var viewModel = new ProductFormVM
        {
            Categories = await GetCategorySelectListAsync()
        };

        return View(viewModel);
    }

    // POST: /Vendor/CreateProduct
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ApprovedVendor]
    public async Task<IActionResult> CreateProduct(ProductFormVM model)
    {
        if (!ModelState.IsValid)
        {
            model.Categories = await GetCategorySelectListAsync(model.CategoryId);
            return View(model);
        }

        var upload = await ProcessImageUploadAsync(model.ImageFile, "products");
        var imageUrl = upload.RelativePath;
        var imageError = upload.ErrorMessage;
        if (imageError != null)
        {
            ModelState.AddModelError("ImageFile", imageError);
            model.Categories = await GetCategorySelectListAsync(model.CategoryId);
            return View(model);
        }

        var product = new Product
        {
            ProductId = Guid.NewGuid(),
            VendorId = CurrentVendor.VendorId,
            CategoryId = model.CategoryId,
            Name = model.Name,
            Description = model.Description,
            Price = model.Price,
            Stock = model.Stock,
            ImageUrl = imageUrl,
            Status = ProductStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Product '{product.Name}' submitted for review.";
        return RedirectToAction(nameof(Products));
    }

    // GET: /Vendor/EditProduct/{id}
    [HttpGet]
    [ApprovedVendor]
    public async Task<IActionResult> EditProduct(Guid id)
    {
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProductId == id && p.VendorId == CurrentVendor.VendorId);

        if (product == null) return NotFound();

        var viewModel = new ProductFormVM
        {
            ProductId = product.ProductId,
            CategoryId = product.CategoryId,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            Stock = product.Stock,
            ImageUrl = product.ImageUrl,
            Categories = await GetCategorySelectListAsync(product.CategoryId)
        };

        return View(viewModel);
    }

    // POST: /Vendor/EditProduct
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ApprovedVendor]
    public async Task<IActionResult> EditProduct(ProductFormVM model)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.ProductId == model.ProductId && p.VendorId == CurrentVendor.VendorId);

        if (product == null) return NotFound();

        if (!ModelState.IsValid)
        {
            model.Categories = await GetCategorySelectListAsync(model.CategoryId);
            return View(model);
        }

        // If the vendor uploaded a new image, replace the old one.
        if (model.ImageFile != null && model.ImageFile.Length > 0)
        {
            var upload = await ProcessImageUploadAsync(model.ImageFile, "products");
            var newImageUrl = upload.RelativePath;
            var imageError = upload.ErrorMessage;
            if (imageError != null)
            {
                ModelState.AddModelError("ImageFile", imageError);
                model.Categories = await GetCategorySelectListAsync(model.CategoryId);
                return View(model);
            }

            if (newImageUrl != null)
            {
                DeleteImageIfExists(product.ImageUrl);
                product.ImageUrl = newImageUrl;
            }
        }
        // No new file: keep product.ImageUrl as-is.

        product.Name = model.Name;
        product.CategoryId = model.CategoryId;
        product.Price = model.Price;
        product.Stock = model.Stock;
        product.Description = model.Description;
        product.Status = ProductStatus.Pending;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Product '{product.Name}' updated and pending re-approval.";
        return RedirectToAction(nameof(Products));
    }

    // POST: /Vendor/UpdateStock
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ApprovedVendor]
    public async Task<IActionResult> UpdateStock(Guid productId, int newStock)
    {
        if (newStock < 0) return RedirectToAction(nameof(Products));

        var rowsAffected = await _context.Products
            .Where(p => p.ProductId == productId && p.VendorId == CurrentVendor.VendorId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, newStock));

        if (rowsAffected > 0)
        {
            TempData["SuccessMessage"] = "Stock updated successfully.";
        }

        return RedirectToAction(nameof(Products));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.Session.Clear();
        return RedirectToAction("Login", "Auth");
    }

    // GET: /Vendor/Orders
    [HttpGet]
    [ApprovedVendor]
    public async Task<IActionResult> Orders(OrderItemStatus? status = null)
    {
        var baseQuery = _context.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Product.VendorId == CurrentVendor.VendorId);

        var pendingCount = await baseQuery.CountAsync(oi => oi.Status == OrderItemStatus.Pending);
        var processingCount = await baseQuery.CountAsync(oi => oi.Status == OrderItemStatus.Processing);
        var shippedCount = await baseQuery.CountAsync(oi => oi.Status == OrderItemStatus.Shipped);
        var deliveredCount = await baseQuery.CountAsync(oi => oi.Status == OrderItemStatus.Delivered);
        var cancelledCount = await baseQuery.CountAsync(oi => oi.Status == OrderItemStatus.Cancelled);

        var query = baseQuery;
        if (status.HasValue)
        {
            query = query.Where(oi => oi.Status == status.Value);
        }

        var items = await query
            .OrderByDescending(oi => oi.Order.CreatedAt)
            .Select(oi => new VendorOrderItemVM
            {
                OrderItemId = oi.OrderItemId,
                OrderId = oi.OrderId,
                ProductName = oi.Product.Name,
                UnitPrice = oi.UnitPrice,
                Quantity = oi.Quantity,
                TotalPrice = oi.TotalPrice,
                Status = oi.Status,
                ShippingCarrier = oi.ShippingCarrier,
                TrackingNumber = oi.TrackingNumber,
                OrderDate = oi.Order.CreatedAt,
                CustomerName = oi.Order.CustomerName ?? "Customer",
                ShippingAddress = oi.Order.ShippingAddress ?? "N/A"
            })
            .ToListAsync();

        var viewModel = new VendorOrderManagementVM
        {
            OrderItems = items,
            CurrentFilter = status,
            PendingCount = pendingCount,
            ProcessingCount = processingCount,
            ShippedCount = shippedCount,
            DeliveredCount = deliveredCount,
            CancelledCount = cancelledCount
        };

        return View(viewModel);
    }

    // POST: /Vendor/UpdateOrderItemStatus
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ApprovedVendor]
    public async Task<IActionResult> UpdateOrderItemStatus(
        Guid orderItemId,
        OrderItemStatus newStatus,
        string? shippingCarrier,
        string? trackingNumber)
    {
        var orderItem = await _context.OrderItems
            .Include(oi => oi.Product)
            .Include(oi => oi.Order)
            .FirstOrDefaultAsync(oi => oi.OrderItemId == orderItemId
                                       && oi.Product.VendorId == CurrentVendor.VendorId);

        if (orderItem == null)
        {
            TempData["ErrorMessage"] = "Order item not found or unauthorized.";
            return RedirectToAction(nameof(Orders));
        }

        var previousStatus = orderItem.Status;
        orderItem.Status = newStatus;

        if (!string.IsNullOrWhiteSpace(shippingCarrier))
            orderItem.ShippingCarrier = shippingCarrier;

        if (!string.IsNullOrWhiteSpace(trackingNumber))
            orderItem.TrackingNumber = trackingNumber;

        // Auto-complete the parent order if every item is Delivered.
        var order = orderItem.Order;
        if (order != null)
        {
            await _context.SaveChangesAsync();

            var allItemStatuses = await _context.OrderItems
                .Where(oi => oi.OrderId == order.OrderId)
                .Select(oi => oi.Status)
                .ToListAsync();

            if (allItemStatuses.All(s => s == OrderItemStatus.Delivered))
            {
                order.Status = OrderStatus.Completed;
                order.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
        else
        {
            await _context.SaveChangesAsync();
        }

        // Send customer email on transitions into Shipped or Delivered.
        if (previousStatus != newStatus &&
            (newStatus == OrderItemStatus.Shipped || newStatus == OrderItemStatus.Delivered) &&
            order != null &&
            !string.IsNullOrWhiteSpace(order.CustomerEmail))
        {
            await SendOrderStatusEmailAsync(order, orderItem, newStatus);
        }

        TempData["SuccessMessage"] = $"Order item status updated to '{newStatus}'.";
        return RedirectToAction(nameof(Orders), new { status = newStatus });
    }

    // --- Private helper ---
    private async Task SendOrderStatusEmailAsync(Order order, OrderItem item, OrderItemStatus newStatus)
    {
        var reference = order.OrderId.ToString()[..8].ToUpperInvariant();
        var productName = item.Product?.Name ?? "Your item";

        string subject;
        string heading;
        string body;

        if (newStatus == OrderItemStatus.Shipped)
        {
            subject = $"Your order #{reference} has shipped";
            heading = "Your Order Has Shipped!";
            body = $@"
            <p>Good news — <strong>{productName}</strong> from your order
            <strong>#{reference}</strong> is on its way.</p>
            <p><strong>Carrier:</strong> {System.Net.WebUtility.HtmlEncode(item.ShippingCarrier ?? "N/A")}<br />
               <strong>Tracking Number:</strong> {System.Net.WebUtility.HtmlEncode(item.TrackingNumber ?? "N/A")}</p>
            <p>Thank you for shopping with Kayane.</p>";
        }
        else // Delivered
        {
            subject = $"Your order #{reference} was delivered";
            heading = "Your Order Was Delivered";
            body = $@"
            <p><strong>{productName}</strong> from your order
            <strong>#{reference}</strong> has been marked as delivered.</p>
            <p>We'd love to hear what you think — you can leave a review from the product page.</p>
            <p>Thank you for shopping with Kayane.</p>";
        }

        var html = $@"
        <div style='font-family: Arial, sans-serif; padding: 20px; max-width: 560px;'>
            <h2 style='color: #4f46e5;'>{heading}</h2>
            <p>Hello {System.Net.WebUtility.HtmlEncode(order.CustomerName ?? "there")},</p>
            {body}
        </div>";

        try
        {
            await _emailService.SendEmailAsync(order.CustomerEmail!, subject, html);
        }
        catch
        {
            // Best-effort: don't fail the request if email fails.
        }
    }

    // GET: /Vendor/EditProfile
    [HttpGet]
    [ApprovedVendor]
    public async Task<IActionResult> EditProfile()
    {
        var vendor = await _context.Vendors
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.VendorId == CurrentVendor.VendorId);

        if (vendor == null) return NotFound();

        return View(vendor);
    }

    // POST: /Vendor/EditProfile
    [HttpPost]
    [ApprovedVendor]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProfile(Vendor model)
    {
        var vendor = await _context.Vendors
            .FirstOrDefaultAsync(v => v.VendorId == CurrentVendor.VendorId);

        if (vendor == null) return NotFound();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        vendor.BusinessName = model.BusinessName;
        vendor.SupportPhone = model.SupportPhone;
        vendor.BankName = model.BankName;
        vendor.AccountNumber = model.AccountNumber;
        vendor.AccountName = model.AccountName;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Profile and bank details updated successfully.";
        return RedirectToAction(nameof(EditProfile));
    }

    #endregion
}