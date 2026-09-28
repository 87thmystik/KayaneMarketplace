using Kayane.Data;
using Kayane.Models;
using Kayane.Services;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kayane.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class RefundsController : Controller
{
    private readonly KayaneDb _context;
    private readonly IAdminAuditService _audit;
    private readonly INotificationService _notifications;

    public RefundsController(
        KayaneDb context,
        IAdminAuditService audit,
        INotificationService notifications)
    {
        _context = context;
        _audit = audit;
        _notifications = notifications;
    }

    // GET: /{adminPrefix}/Refunds
    [HttpGet]
    public async Task<IActionResult> Index(string filter = "pending", int page = 1)
    {
        if (filter != "pending" && filter != "processed" && filter != "all")
            filter = "pending";

        const int pageSize = 25;
        page = Math.Max(1, page);

        var baseQuery = _context.Payments
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Refunded);

        var pendingCount = await baseQuery.CountAsync(p => p.RefundedAt == null);
        var processedCount = await baseQuery.CountAsync(p => p.RefundedAt != null);

        var query = baseQuery;
        if (filter == "pending")
            query = query.Where(p => p.RefundedAt == null);
        else if (filter == "processed")
            query = query.Where(p => p.RefundedAt != null);

        var totalItems = await query.CountAsync();

        var raw = await query
            .Include(p => p.Order)
                .ThenInclude(o => o!.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(prod => prod!.Vendor)
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Resolve admin names for processed refunds in one query.
        var adminIds = raw
            .Where(p => p.RefundedByAdminId.HasValue)
            .Select(p => p.RefundedByAdminId!.Value)
            .Distinct()
            .ToList();

        var adminNames = await _context.Users
            .AsNoTracking()
            .Where(u => adminIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.Name })
            .ToDictionaryAsync(u => u.UserId, u => u.Name);

        var items = raw.Select(p =>
        {
            var order = p.Order;

            var vendors = order?.OrderItems
                .Where(oi => oi.Product?.Vendor != null)
                .GroupBy(oi => new { oi.Product!.VendorId, oi.Product.Vendor!.BusinessName })
                .Select(g => new RefundVendorLineVM
                {
                    VendorName = g.Key.BusinessName,
                    Amount = g.Sum(oi => oi.TotalPrice)
                })
                .ToList() ?? new List<RefundVendorLineVM>();

            string? refundedByName = null;
            if (p.RefundedByAdminId.HasValue)
            {
                adminNames.TryGetValue(p.RefundedByAdminId.Value, out var name);
                refundedByName = name;
            }

            return new RefundQueueItemVM
            {
                PaymentId = p.PaymentId,
                OrderId = p.OrderId,
                PaymentReference = p.PaymentReference ?? string.Empty,
                Amount = p.Amount,
                BuyerName = order?.CustomerName ?? "Unknown",
                BuyerEmail = order?.CustomerEmail ?? string.Empty,
                PaymentCreatedAt = p.CreatedAt,
                CancelledAt = order?.UpdatedAt,
                CancellationReason = "Buyer/vendor cancellation",
                Vendors = vendors,
                IsProcessed = p.RefundedAt.HasValue,
                RefundedAt = p.RefundedAt,
                RefundedByName = refundedByName
            };
        }).ToList();

        var vm = new RefundQueueVM
        {
            Items = items,
            CurrentFilter = filter,
            PendingCount = pendingCount,
            ProcessedCount = processedCount,
            PendingTotal = await baseQuery.Where(p => p.RefundedAt == null).SumAsync(p => p.Amount),
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = totalItems
        };

        return View(vm);
    }

    // POST: /{adminPrefix}/Refunds/MarkProcessed
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkProcessed(Guid paymentId, string? note)
    {
        var payment = await _context.Payments
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

        if (payment == null) return NotFound();

        if (payment.Status != PaymentStatus.Refunded)
        {
            TempData["ErrorMessage"] = "This payment isn't flagged for refund.";
            return RedirectToAction(nameof(Index));
        }

        if (payment.RefundedAt.HasValue)
        {
            TempData["ErrorMessage"] = "This refund has already been processed.";
            return RedirectToAction(nameof(Index));
        }

        var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(adminIdStr, out var adminId))
            return Challenge();

        payment.RefundedAt = DateTime.UtcNow;
        payment.RefundedByAdminId = adminId;
        payment.UpdatedAt = DateTime.UtcNow;

        await _audit.LogAsync("refund_processed", "Payment", payment.PaymentId, new
        {
            orderId = payment.OrderId,
            amount = payment.Amount,
            reference = payment.PaymentReference,
            note = note
        });

        if (payment.Order != null)
        {
            var reference = payment.Order.OrderId.ToString()[..8].ToUpperInvariant();
            await _notifications.NotifyAsync(
                payment.Order.UserId,
                "Refund processed",
                $"Your refund of {payment.Amount:C} for order #{reference} has been processed.",
                NotificationType.OrderUpdate);
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] =
            $"Refund of {payment.Amount:C} marked as processed and buyer notified.";

        return RedirectToAction(nameof(Index));
    }
}