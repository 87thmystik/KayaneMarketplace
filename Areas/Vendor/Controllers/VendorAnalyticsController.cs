using Kayane.Data;
using Kayane.Filters;
using Kayane.Models;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kayane.Areas.VendorPanel.Controllers;

[Area("Vendor")]
[Authorize]
[ApprovedVendor]
public class VendorAnalyticsController : Controller
{
    private readonly KayaneDb _context;

    public VendorAnalyticsController(KayaneDb context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var vendor = HttpContext.Items["CurrentVendor"] as Vendor;
        if (vendor == null) return Challenge();

        var orderItems = await _context.OrderItems
            .Include(oi => oi.Order)
            .Include(oi => oi.Product)
            .AsNoTracking()
            .Where(oi => oi.Product.VendorId == vendor.VendorId && oi.Order.PaymentStatus == PaymentStatus.Success)
            .ToListAsync();

        var currentMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        var viewModel = new VendorAnalyticsVM
        {
            TotalRevenue = orderItems.Sum(oi => oi.TotalPrice),
            CurrentMonthRevenue = orderItems.Where(oi => oi.Order.CreatedAt >= currentMonthStart).Sum(oi => oi.TotalPrice),
            TotalOrdersFulfilled = orderItems.Count(oi => oi.Status == OrderItemStatus.Completed),
            PendingOrdersCount = orderItems.Count(oi => oi.Status == OrderItemStatus.Pending),
            TopProducts = orderItems
                .GroupBy(oi => oi.Product.Name)
                .Select(g => new TopProductMetricVM
                {
                    ProductName = g.Key,
                    UnitsSold = g.Sum(x => x.Quantity),
                    RevenueGenerated = g.Sum(x => x.TotalPrice)
                })
                .OrderByDescending(p => p.RevenueGenerated)
                .Take(5)
                .ToList()
        };

        return View(viewModel);
    }
}