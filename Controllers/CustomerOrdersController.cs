using System.Security.Claims;
using Kayane.Data;
using Kayane.Models;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kayane.Controllers;

[Authorize]
public class CustomerOrdersController : Controller
{
    private readonly KayaneDb _context;

    public CustomerOrdersController(KayaneDb context)
    {
        _context = context;
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

    // Roll-up rule: pick the least-advanced state any item is in.
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