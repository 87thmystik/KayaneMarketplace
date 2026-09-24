using Kayane.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kayane.ViewComponents;

public class UserAvatarViewComponent : ViewComponent
{
    private readonly KayaneDb _context;

    public UserAvatarViewComponent(KayaneDb context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userIdStr = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Content(string.Empty);

        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new { u.Name, u.AvatarUrl })
            .FirstOrDefaultAsync();

        if (user == null) return Content(string.Empty);

        return View(user);
    }
}