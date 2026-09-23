using System.Security.Claims;
using Kayane.Data;
using Kayane.Models;
using Kayane.Services;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kayane.Controllers;

[Authorize]
public class CustomerOrdersController : Controller
{
    private readonly KayaneDb _context;
    private readonly INotificationService _notifications;
    private readonly IEmailService _emailService;
    private readonly ILogger<CustomerOrdersController> _logger;

    public CustomerOrdersController(
        KayaneDb context,
        INotificationService notifications,
        IEmailService emailService,
        ILogger<CustomerOrdersController> logger)
    {
        _context = context;
        _notifications = notifications;
        _emailService = emailService;
        _logger = logger;
    }

    // GET: /CustomerOrders
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Challenge();

        var orders = await _context.Orders
            .Include(o => o.OrderItems)
            .AsNoTracking()
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var summaries = orders.Select(o => new CustomerOrderSummaryVM
        {
            OrderId = o.OrderId,
            CreatedAt = o.CreatedAt,
            TotalAmount = o.TotalAmount,
            Status = o.Status,
            PaymentStatus = o.PaymentStatus,
            ItemCount = o.OrderItems.Sum(i => i.Quantity),
            ShippingState = ComputeShippingState(o.OrderItems.Select(i => i.Status))
        }).ToList();

        return View(summaries);
    }

    // GET: /CustomerOrders/Details/{id}
    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Challenge();

        var order = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

        if (order == null) return NotFound();

        var vm = new CustomerOrderDetailVM
        {
            OrderId = order.OrderId,
            CreatedAt = order.CreatedAt,
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            PaymentStatus = order.PaymentStatus,
            PaymentMethod = order.PaymentMethod,
            CustomerName = order.CustomerName ?? string.Empty,
            CustomerEmail = order.CustomerEmail ?? string.Empty,
            CustomerPhone = order.CustomerPhone ?? string.Empty,
            ShippingAddress = order.ShippingAddress ?? string.Empty,
            Items = order.OrderItems.Select(oi => new OrderItemVM
            {
                ProductId = oi.ProductId,
                ProductName = oi.Product?.Name ?? "Product",
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice,
                TotalPrice = oi.TotalPrice,
                Status = oi.Status,
                ShippingCarrier = oi.ShippingCarrier,
                TrackingNumber = oi.TrackingNumber
            }).ToList()
        };

        return View(vm);
    }

    // GET: /CustomerOrders/Cancel/{id}
    [HttpGet]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Challenge();

        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

        if (order == null) return NotFound();

        if (!CanCancelOrder(order, out var reason))
        {
            TempData["ErrorMessage"] = reason;
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(new CancelOrderVM { OrderId = id });
    }

    // POST: /CustomerOrders/Cancel
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(CancelOrderVM model)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Challenge();

        var order = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.OrderId == model.OrderId && o.UserId == userId);

        if (order == null) return NotFound();

        if (!CanCancelOrder(order, out var cannotReason))
        {
            TempData["ErrorMessage"] = cannotReason;
            return RedirectToAction(nameof(Details), new { id = model.OrderId });
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await ReverseOrderAsync(order, model.Reason, "Buyer cancelled");

        // Notify buyer + vendors
        await _notifications.NotifyAsync(
            order.UserId,
            "Order cancelled",
            $"Your order #{order.OrderId.ToString()[..8].ToUpperInvariant()} has been cancelled. " +
            (order.PaymentStatus == PaymentStatus.Success
                ? "A refund is being processed."
                : "No payment was taken."),
            NotificationType.OrderUpdate);

        var vendorUserIds = await _context.Vendors
            .Where(v => order.OrderItems.Select(i => i.Product!.VendorId).Contains(v.VendorId))
            .Select(v => v.UserId)
            .Distinct()
            .ToListAsync();

        if (vendorUserIds.Any())
        {
            await _notifications.NotifyManyAsync(
                vendorUserIds,
                "Order cancelled by buyer",
                $"Order #{order.OrderId.ToString()[..8].ToUpperInvariant()} was cancelled by the buyer.",
                NotificationType.OrderUpdate);
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Order cancelled successfully.";
        return RedirectToAction(nameof(Details), new { id = model.OrderId });
    }

    // ============================================================
    // Helpers
    // ============================================================

    private static bool CanCancelOrder(Order order, out string reason)
    {
        reason = "";

        if (order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Completed)
        {
            reason = "This order can no longer be cancelled.";
            return false;
        }

        // Refuse if any item is already being processed or beyond
        var blocker = order.OrderItems.FirstOrDefault(i =>
            i.Status == OrderItemStatus.Processing ||
            i.Status == OrderItemStatus.Shipped ||
            i.Status == OrderItemStatus.Delivered ||
            i.Status == OrderItemStatus.Completed);

        if (blocker != null)
        {
            reason = "This order can't be cancelled — the vendor has already started fulfilling it.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reverse an order: cancel items, restore stock, debit wallets if paid.
    /// Does NOT call SaveChanges — the caller saves.
    /// </summary>
    private async Task ReverseOrderAsync(Order order, string reason, string source)
    {
        // Restore stock for all items and mark them cancelled.
        foreach (var item in order.OrderItems)
        {
            if (item.Product != null)
            {
                item.Product.Stock += item.Quantity;
            }
            item.Status = OrderItemStatus.Cancelled;
        }

        // Debit vendor wallets if the buyer actually paid.
        if (order.PaymentStatus == PaymentStatus.Success)
        {
            foreach (var item in order.OrderItems)
            {
                if (item.Product == null) continue;

                var vendorId = item.Product.VendorId;

                var wallet = await _context.VendorWallets
                    .FirstOrDefaultAsync(w => w.VendorId == vendorId);

                if (wallet == null) continue;

                // Debit but never go negative — clamp at zero.
                var debit = Math.Min(wallet.Balance, item.TotalPrice);
                wallet.Balance -= debit;
                wallet.UpdatedAt = DateTime.UtcNow;
            }

            // Mark payment for manual refund (9PSB refund API is not wired).
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.OrderId == order.OrderId);

            if (payment != null)
            {
                payment.Status = PaymentStatus.Refunded;
                payment.UpdatedAt = DateTime.UtcNow;
            }
        }

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Order {OrderId} cancelled by {Source}. Reason: {Reason}",
            order.OrderId, source, reason);
    }

    private static string ComputeShippingState(IEnumerable<OrderItemStatus> statuses)
    {
        var list = statuses.ToList();
        if (list.Count == 0) return "Processing";
        if (list.All(s => s == OrderItemStatus.Delivered)) return "Delivered";
        if (list.All(s => s == OrderItemStatus.Cancelled)) return "Cancelled";
        if (list.Any(s => s == OrderItemStatus.Pending)) return "Awaiting Fulfillment";
        if (list.Any(s => s == OrderItemStatus.Processing)) return "Processing";
        if (list.Any(s => s == OrderItemStatus.Shipped) &&
            list.Any(s => s == OrderItemStatus.Delivered)) return "Partially Delivered";
        if (list.All(s => s == OrderItemStatus.Shipped)) return "Shipped";
        return "Processing";
    }
}