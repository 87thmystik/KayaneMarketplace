using Kayane.Data;
using Kayane.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace Kayane.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ApprovedVendorAttribute : TypeFilterAttribute
{
    public ApprovedVendorAttribute() : base(typeof(ApprovedVendorFilter)) { }
}

public class ApprovedVendorFilter : IAsyncActionFilter
{
    private readonly KayaneDb _context;
    private readonly IMemoryCache _cache;

    public ApprovedVendorFilter(KayaneDb context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdStr, out var userId))
        {
            context.Result = new ChallengeResult();
            return;
        }

        var cacheKey = $"vendor_user_{userId}";

        var vendor = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(15))
                 .SetSlidingExpiration(TimeSpan.FromMinutes(5));

            return await _context.Vendors
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.UserId == userId);
        });

        // 1. Not a vendor — send to public vendor registration
        if (vendor == null)
        {
            context.Result = new RedirectToActionResult(
                "RegisterVendor", "Auth", new { area = "" });
            return;
        }

        // 2. Pending/rejected/suspended — send to status page
        if (vendor.Status != VendorStatus.Active)
        {
            context.Result = new RedirectToActionResult(
                "ApplicationStatus", "Vendor", new { area = "Vendor" });
            return;
        }

        context.HttpContext.Items["CurrentVendor"] = vendor;

        await next();
    }
}