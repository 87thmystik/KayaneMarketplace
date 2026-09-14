using Kayane.Data;
using Kayane.Models;
using Kayane.Services;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kayane.Areas.Admin.Controllers;


[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AdminProductsController : Controller
{
    private readonly KayaneDb _context;
    private readonly IEmailService _emailService;

    public AdminProductsController(KayaneDb context, IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    // GET: /AdminProducts
    [HttpGet]
    public async Task<IActionResult> Index(ProductStatus status = ProductStatus.Pending)
    {
        var baseQuery = _context.Products.AsNoTracking();

        var pendingCount = await baseQuery.CountAsync(p => p.Status == ProductStatus.Pending);
        var approvedCount = await baseQuery.CountAsync(p => p.Status == ProductStatus.Approved);
        var rejectedCount = await baseQuery.CountAsync(p => p.Status == ProductStatus.Rejected);

        var products = await baseQuery
            .Where(p => p.Status == status)
            .Include(p => p.Category)
            .Include(p => p.Vendor)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new AdminProductListItemVM
            {
                ProductId = p.ProductId,
                Name = p.Name,
                CategoryName = p.Category != null ? p.Category.Name : "Uncategorized",
                VendorBusinessName = p.Vendor != null ? p.Vendor.BusinessName : "Unknown Vendor",
                Price = p.Price,
                Stock = p.Stock,
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                Description = p.Description
            })
            .ToListAsync();

        var viewModel = new ModerationQueueVM
        {
            Products = products,
            CurrentFilter = status,
            PendingCount = pendingCount,
            ApprovedCount = approvedCount,
            RejectedCount = rejectedCount
        };

        return View(viewModel);
    }

    // POST: /AdminProducts/Approve/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id)
    {
        var product = await _context.Products
            .Include(p => p.Vendor)
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product == null)
        {
            TempData["ErrorMessage"] = "Product not found.";
            return RedirectToAction(nameof(Index), new { status = ProductStatus.Pending });
        }

        product.Status = ProductStatus.Approved;
        await _context.SaveChangesAsync();

        // Send Approval Email Notification
        if (product.Vendor != null && !string.IsNullOrWhiteSpace(product.Vendor.SupportEmail))
        {
            var subject = $"Product Approved: {product.Name}";
            var body = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px;'>
                    <h2 style='color: #28a745;'>Product Approved!</h2>
                    <p>Dear <strong>{product.Vendor.BusinessName}</strong>,</p>
                    <p>Your product <strong>{product.Name}</strong> has been reviewed and approved by our moderation team.</p>
                    <p>It is now live on the Kayane marketplace for customers to purchase.</p>
                </div>";

            await _emailService.SendEmailAsync(product.Vendor.SupportEmail, subject, body);
        }

        TempData["SuccessMessage"] = $"Product '{product.Name}' approved and vendor notified.";
        return RedirectToAction(nameof(Index), new { status = ProductStatus.Pending });
    }

    // POST: /AdminProducts/Reject
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid productId, string? rejectionReason)
    {
        var product = await _context.Products
            .Include(p => p.Vendor)
            .FirstOrDefaultAsync(p => p.ProductId == productId);

        if (product == null)
        {
            TempData["ErrorMessage"] = "Product not found.";
            return RedirectToAction(nameof(Index));
        }

        product.Status = ProductStatus.Rejected;
        await _context.SaveChangesAsync();

        // Send Rejection Email Notification
        if (product.Vendor != null && !string.IsNullOrWhiteSpace(product.Vendor.SupportEmail))
        {
            var reasonText = !string.IsNullOrWhiteSpace(rejectionReason)
                ? rejectionReason
                : "Does not meet listing guidelines.";

            var subject = $"Product Status Update: {product.Name}";
            var body = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px;'>
                    <h2 style='color: #dc3545;'>Product Review Update</h2>
                    <p>Dear <strong>{product.Vendor.BusinessName}</strong>,</p>
                    <p>Your submission for <strong>{product.Name}</strong> was not approved for listing.</p>
                    <p><strong>Reason for Rejection:</strong></p>
                    <blockquote style='background: #f8f9fa; border-left: 4px solid #dc3545; padding: 10px;'>
                        {reasonText}
                    </blockquote>
                    <p>You can update the details from your Vendor Dashboard and resubmit for review.</p>
                </div>";

            await _emailService.SendEmailAsync(product.Vendor.SupportEmail, subject, body);
        }

        TempData["SuccessMessage"] = $"Product '{product.Name}' rejected and vendor notified.";
        return RedirectToAction(nameof(Index), new { status = ProductStatus.Pending });
    }
}