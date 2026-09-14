using System.Security.Claims;
using Kayane.Data;
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
        {
            return Challenge();
        }

        var orders = await _context.Orders
            .Include(o => o.OrderItems)
            .AsNoTracking()
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new CustomerOrderSummaryVM
            {
                OrderId = o.OrderId,
                CreatedAt = o.CreatedAt,
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                PaymentStatus = o.PaymentStatus,
                ItemCount = o.OrderItems.Sum(i => i.Quantity)
            })
            .ToListAsync();

        return View(orders);
    }

    // GET: /CustomerOrders/Details/{id}
    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Challenge();
        }

        var order = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

        if (order == null)
        {
            return NotFound();
        }

        var viewModel = new CustomerOrderDetailVM
        {
            OrderId = order.OrderId,
            CreatedAt = order.CreatedAt,
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            PaymentStatus = order.PaymentStatus,
            PaymentMethod = order.PaymentMethod,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            CustomerPhone = order.CustomerPhone,
            ShippingAddress = order.ShippingAddress,
            Items = order.OrderItems.Select(oi => new OrderItemVM
            {
                ProductId = oi.ProductId,
                ProductName = oi.Product?.Name ?? "Product",
                Quantity = oi.Quantity,
                UnitPrice = oi.Price,
                TotalPrice = oi.Price * oi.Quantity
            }).ToList()
        };

        return View(viewModel);
    }
}