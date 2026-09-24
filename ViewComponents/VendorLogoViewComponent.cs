using Kayane.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kayane.ViewComponents;

public class VendorLogoViewComponent : ViewComponent
{
    private readonly KayaneDb _context;

    public VendorLogoViewComponent(KayaneDb context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userIdStr = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Content(string.Empty);

        var vendor = await _context.Vendors
            .AsNoTracking()
            .Where(v => v.UserId == userId)
            .Select(v => new { v.BusinessName, v.LogoUrl })
            .FirstOrDefaultAsync();

        if (vendor == null) return Content(string.Empty);

        return View(vendor);
    }
}