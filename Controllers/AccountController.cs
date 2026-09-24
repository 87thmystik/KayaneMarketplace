using Kayane.Data;
using Kayane.Models;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kayane.Controllers;

[Authorize]
public class AccountController : Controller
{
    private readonly KayaneDb _context;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        KayaneDb context,
        IWebHostEnvironment environment,
        ILogger<AccountController> logger)
    {
        _context = context;
        _environment = environment;
        _logger = logger;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // GET: /Account
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (CurrentUserId is not { } userId) return Challenge();

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return NotFound();

        var orders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.OrderItems)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var active = new[]
        {
            OrderStatus.Pending, OrderStatus.Paid,
            OrderStatus.Processing, OrderStatus.Shipped
        };

        var vm = new AccountDashboardVM
        {
            Name = user.Name,
            Email = user.Email,
            Phone = user.Phone,
            MemberSince = user.CreatedAt,
            TotalOrders = orders.Count,
            ActiveOrders = orders.Count(o => active.Contains(o.Status)),
            DeliveredOrders = orders.Count(o => o.Status == OrderStatus.Completed),
            AddressCount = await _context.BuyerAddresses.CountAsync(a => a.UserId == userId),
            ReviewCount = await _context.ProductReviews.CountAsync(r => r.UserId == userId),
            UnreadNotifications = await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead),
            RecentOrders = orders.Take(3).Select(o => new AccountDashboardVM.RecentOrderItemVM
            {
                OrderId = o.OrderId,
                CreatedAt = o.CreatedAt,
                TotalAmount = o.TotalAmount,
                Status = o.Status.ToString(),
                ItemCount = o.OrderItems.Sum(i => i.Quantity)
            }).ToList()
        };

        return View(vm);
    }
    // POST: /Account/UploadAvatar
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAvatar(IFormFile? avatarFile)
    {
        if (CurrentUserId is not { } userId) return Challenge();

        if (avatarFile == null || avatarFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Please choose an image.";
            return RedirectToAction(nameof(Profile));
        }

        const long maxBytes = 5 * 1024 * 1024;
        if (avatarFile.Length > maxBytes)
        {
            TempData["ErrorMessage"] = "Image must be smaller than 5 MB.";
            return RedirectToAction(nameof(Profile));
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            TempData["ErrorMessage"] = "Only .jpg, .jpeg, .png, and .webp images are allowed.";
            return RedirectToAction(nameof(Profile));
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return NotFound();

        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "avatars");
        Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await avatarFile.CopyToAsync(stream);
        }

        // Delete old avatar if it exists and is under /uploads/
        if (!string.IsNullOrWhiteSpace(user.AvatarUrl) &&
            user.AvatarUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            var oldPath = Path.Combine(
                _environment.WebRootPath,
                user.AvatarUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            try { if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath); }
            catch { /* best-effort */ }
        }

        user.AvatarUrl = $"/uploads/avatars/{uniqueFileName}";
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Avatar updated.";
        return RedirectToAction(nameof(Profile));
    }

    // GET: /Account/Profile
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        if (CurrentUserId is not { } userId) return Challenge();
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return NotFound();

        return View(new AccountProfileVM
        {
            Name = user.Name,
            Email = user.Email,
            Phone = user.Phone,
            AvatarUrl = user.AvatarUrl
        });
    }

    // POST: /Account/Profile
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(AccountProfileVM model)
    {
        if (CurrentUserId is not { } userId) return Challenge();
        if (!ModelState.IsValid) return View(model);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return NotFound();

        var normalized = model.Email.Trim().ToLowerInvariant();

        if (!string.Equals(user.Email, normalized, StringComparison.OrdinalIgnoreCase))
        {
            var taken = await _context.Users.AnyAsync(u => u.UserId != userId && u.Email == normalized);
            if (taken)
            {
                ModelState.AddModelError(nameof(model.Email), "That email is already in use.");
                return View(model);
            }
            user.Email = normalized;
        }

        user.Name = model.Name.Trim();
        user.Phone = (model.Phone ?? "").Trim();

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Profile updated.";
        return RedirectToAction(nameof(Profile));
    }

    // GET: /Account/ChangePassword
    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordVM());

    // POST: /Account/ChangePassword
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordVM model)
    {
        if (CurrentUserId is not { } userId) return Challenge();
        if (!ModelState.IsValid) return View(model);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return NotFound();

        var hasher = new PasswordHasher<User>();
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword);

        if (result != PasswordVerificationResult.Success &&
            result != PasswordVerificationResult.SuccessRehashNeeded)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect.");
            return View(model);
        }

        user.PasswordHash = hasher.HashPassword(user, model.NewPassword);
        user.MustChangePassword = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} changed password.", userId);

        TempData["SuccessMessage"] = "Password changed successfully.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Account/Addresses
    [HttpGet]
    public async Task<IActionResult> Addresses()
    {
        if (CurrentUserId is not { } userId) return Challenge();

        var addresses = await _context.BuyerAddresses
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();

        return View(addresses);
    }

    // GET: /Account/AddressForm/{id?}
    [HttpGet]
    public async Task<IActionResult> AddressForm(Guid? id)
    {
        if (CurrentUserId is not { } userId) return Challenge();

        if (id == null) return View(new BuyerAddressFormVM());

        var a = await _context.BuyerAddresses.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AddressId == id && x.UserId == userId);
        if (a == null) return NotFound();

        return View(new BuyerAddressFormVM
        {
            AddressId = a.AddressId,
            Label = a.Label,
            RecipientName = a.RecipientName,
            Phone = a.Phone,
            AddressLine1 = a.AddressLine1,
            AddressLine2 = a.AddressLine2,
            City = a.City,
            State = a.State,
            IsDefault = a.IsDefault
        });
    }

    // POST: /Account/AddressForm
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddressForm(BuyerAddressFormVM model)
    {
        if (CurrentUserId is not { } userId) return Challenge();
        if (!ModelState.IsValid) return View(model);

        BuyerAddress addr;

        if (model.AddressId.HasValue)
        {
            var existing = await _context.BuyerAddresses
                .FirstOrDefaultAsync(a => a.AddressId == model.AddressId.Value && a.UserId == userId);
            if (existing == null) return NotFound();
            addr = existing;
        }
        else
        {
            addr = new BuyerAddress
            {
                AddressId = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            _context.BuyerAddresses.Add(addr);
        }

        if (model.IsDefault)
        {
            var others = await _context.BuyerAddresses
                .Where(a => a.UserId == userId && a.AddressId != addr.AddressId)
                .ToListAsync();
            foreach (var o in others) o.IsDefault = false;
        }

        addr.Label = model.Label.Trim();
        addr.RecipientName = model.RecipientName.Trim();
        addr.Phone = model.Phone.Trim();
        addr.AddressLine1 = model.AddressLine1.Trim();
        addr.AddressLine2 = model.AddressLine2?.Trim();
        addr.City = model.City.Trim();
        addr.State = model.State.Trim();
        addr.IsDefault = model.IsDefault;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = model.AddressId.HasValue ? "Address updated." : "Address added.";
        return RedirectToAction(nameof(Addresses));
    }

    // POST: /Account/SetDefaultAddress
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefaultAddress(Guid id)
    {
        if (CurrentUserId is not { } userId) return Challenge();

        var addresses = await _context.BuyerAddresses.Where(a => a.UserId == userId).ToListAsync();
        foreach (var a in addresses) a.IsDefault = a.AddressId == id;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Default address updated.";
        return RedirectToAction(nameof(Addresses));
    }

    // POST: /Account/DeleteAddress
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAddress(Guid id)
    {
        if (CurrentUserId is not { } userId) return Challenge();

        var a = await _context.BuyerAddresses
            .FirstOrDefaultAsync(x => x.AddressId == id && x.UserId == userId);
        if (a == null) return NotFound();

        _context.BuyerAddresses.Remove(a);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Address removed.";
        return RedirectToAction(nameof(Addresses));
    }

    // GET: /Account/Reviews
    [HttpGet]
    public async Task<IActionResult> Reviews()
    {
        if (CurrentUserId is not { } userId) return Challenge();

        var reviews = await _context.ProductReviews
            .AsNoTracking()
            .Include(r => r.Product)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new AccountReviewItemVM
            {
                ReviewId = r.ReviewId,
                ProductId = r.ProductId,
                ProductName = r.Product != null ? r.Product.Name : "Product",
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        return View(reviews);
    }

    // GET: /Account/Notifications
    [HttpGet]
    public async Task<IActionResult> Notifications()
    {
        if (CurrentUserId is not { } userId) return Challenge();

        var notifications = await _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new AccountNotificationItemVM
            {
                NotificationId = n.NotificationId,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type.ToString(),
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();

        return View(notifications);
    }

    // POST: /Account/MarkNotificationRead
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkNotificationRead(Guid id)
    {
        if (CurrentUserId is not { } userId) return Challenge();

        var n = await _context.Notifications
            .FirstOrDefaultAsync(x => x.NotificationId == id && x.UserId == userId);
        if (n == null) return NotFound();

        n.IsRead = true;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Notifications));
    }

    // POST: /Account/MarkAllNotificationsRead
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllNotificationsRead()
    {
        if (CurrentUserId is not { } userId) return Challenge();

        var unread = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread) n.IsRead = true;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "All notifications marked as read.";
        return RedirectToAction(nameof(Notifications));
    }
}