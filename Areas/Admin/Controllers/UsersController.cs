using Kayane.Data;
using Kayane.Models;
using Kayane.Services;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kayane.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly KayaneDb _context;
    private readonly IAdminAuditService _audit;
    private readonly INotificationService _notifications;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        KayaneDb context,
        IAdminAuditService audit,
        INotificationService notifications,
        ILogger<UsersController> logger)
    {
        _context = context;
        _audit = audit;
        _notifications = notifications;
        _logger = logger;
    }

    // GET: /{adminPrefix}/Users
    [HttpGet]
    public async Task<IActionResult> Index(
        UserRole? roleFilter,
        string? statusFilter,
        string? searchKeyword,
        int page = 1)
    {
        const int pageSize = 20;
        page = Math.Max(1, page);

        var query = _context.Users.AsNoTracking();

        switch (statusFilter)
        {
            case "banned":
                query = query.Where(u => u.IsBanned && u.DeletedAt == null);
                break;
            case "deleted":
                query = query.Where(u => u.DeletedAt != null);
                break;
            case "all":
                break;
            case "active":
            default:
                query = query.Where(u => !u.IsBanned && u.DeletedAt == null);
                statusFilter = "active";
                break;
        }

        if (roleFilter.HasValue)
            query = query.Where(u => u.Role == roleFilter.Value);

        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            var term = searchKeyword.Trim();
            query = query.Where(u =>
                u.Name.Contains(term) ||
                u.Email.Contains(term));
        }

        var totalItems = await query.CountAsync();

        var raw = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.UserId,
                u.Name,
                u.Email,
                u.Phone,
                u.Role,
                u.CreatedAt,
                u.IsBanned,
                u.BannedReason,
                u.MustChangePassword,
                u.IsSuperAdmin,
                DeletedAt = u.DeletedAt
            })
            .ToListAsync();

        var userIds = raw.Select(r => r.UserId).ToList();

        var orderStats = await _context.Orders
            .AsNoTracking()
            .Where(o => userIds.Contains(o.UserId))
            .GroupBy(o => o.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalOrders = g.Count(),
                TotalSpent = g.Where(o => o.PaymentStatus == PaymentStatus.Success)
                              .Sum(o => (decimal?)o.TotalAmount) ?? 0m
            })
            .ToListAsync();

        var statsDict = orderStats.ToDictionary(s => s.UserId);

        var users = raw.Select(r =>
        {
            statsDict.TryGetValue(r.UserId, out var stats);
            return new AdminUserListItemVM
            {
                UserId = r.UserId,
                Name = r.Name,
                Email = r.Email,
                Phone = r.Phone,
                Role = r.Role,
                CreatedAt = r.CreatedAt,
                IsBanned = r.IsBanned,
                BannedReason = r.BannedReason,
                MustChangePassword = r.MustChangePassword,
                IsDeleted = r.DeletedAt.HasValue,
                IsSuperAdmin = r.IsSuperAdmin,
                TotalOrders = stats?.TotalOrders ?? 0,
                TotalSpent = stats?.TotalSpent ?? 0m
            };
        }).ToList();

        var vm = new AdminUserListVM
        {
            Users = users,
            RoleFilter = roleFilter,
            StatusFilter = statusFilter,
            SearchKeyword = searchKeyword,
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = totalItems
        };
        vm.CurrentUserIsSuperAdmin = await CurrentUserIsSuperAdminAsync();
        return View(vm);
    }

    // GET: /{adminPrefix}/Users/CreateAdmin
    [HttpGet]
    public async Task<IActionResult> CreateAdmin()
    {
        if (!await CurrentUserIsSuperAdminAsync())
        {
            TempData["ErrorMessage"] = "Only the Super Admin can create new admins.";
            return RedirectToAction(nameof(Index));
        }

        return View(new AdminCreateVM());
    }

    // POST: /{adminPrefix}/Users/CreateAdmin
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAdmin(AdminCreateVM model)
    {
        if (!await CurrentUserIsSuperAdminAsync())
        {
            TempData["ErrorMessage"] = "Only the Super Admin can create new admins.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid) return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();

        if (await _context.Users.AnyAsync(u => u.Email == normalizedEmail))
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
            return View(model);
        }

        var adminUser = new User
        {
            UserId = Guid.NewGuid(),
            Name = model.Name.Trim(),
            Email = normalizedEmail,
            Phone = (model.Phone ?? "").Trim(),
            Role = UserRole.Admin,
            IsSuperAdmin = false,
            EmailVerified = true,              // Super Admin vouches for them
            EmailVerifiedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var hasher = new PasswordHasher<User>();
        adminUser.PasswordHash = hasher.HashPassword(adminUser, model.Password);

        _context.Users.Add(adminUser);

        await _audit.LogAsync("admin_created", "User", adminUser.UserId, new
        {
            email = adminUser.Email,
            name = adminUser.Name
        });

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Super Admin {AdminId} created new admin {NewAdminId} ({Email})",
            GetAdminId(), adminUser.UserId, adminUser.Email);

        TempData["SuccessMessage"] = $"Admin account created for {adminUser.Name}.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /{adminPrefix}/Users/Ban
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ban(Guid userId, string? reason)
    {
        var target = await _context.Users.FindAsync(userId);
        if (target == null) return NotFound();

        var adminId = GetAdminId();
        if (adminId == null) return Challenge();

        if (target.UserId == adminId.Value)
        {
            TempData["ErrorMessage"] = "You can't ban yourself.";
            return RedirectToAction(nameof(Index));
        }

        // Super Admin is untouchable
        if (target.IsSuperAdmin)
        {
            TempData["ErrorMessage"] = "The Super Admin cannot be banned.";
            return RedirectToAction(nameof(Index));
        }

        // Regular admins can only be banned by the Super Admin
        if (target.Role == UserRole.Admin && !await CurrentUserIsSuperAdminAsync())
        {
            TempData["ErrorMessage"] = "Only the Super Admin can ban other admins.";
            return RedirectToAction(nameof(Index));
        }

        target.IsBanned = true;
        target.BannedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        await _audit.LogAsync("user_banned", "User", target.UserId, new
        {
            email = target.Email,
            role = target.Role.ToString(),
            reason = target.BannedReason
        });

        await _context.SaveChangesAsync();

        _logger.LogInformation("Admin {AdminId} banned user {UserId}", adminId, target.UserId);

        TempData["SuccessMessage"] = $"{target.Name} has been banned.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /{adminPrefix}/Users/Unban
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unban(Guid userId)
    {
        var target = await _context.Users.FindAsync(userId);
        if (target == null) return NotFound();

        // Regular admins can only be unbanned by the Super Admin
        if (target.Role == UserRole.Admin && !await CurrentUserIsSuperAdminAsync())
        {
            TempData["ErrorMessage"] = "Only the Super Admin can unban other admins.";
            return RedirectToAction(nameof(Index));
        }

        target.IsBanned = false;
        target.BannedReason = null;

        await _audit.LogAsync("user_unbanned", "User", target.UserId, new
        {
            email = target.Email
        });

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"{target.Name} has been unbanned.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /{adminPrefix}/Users/ForcePasswordReset
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForcePasswordReset(Guid userId)
    {
        var target = await _context.Users.FindAsync(userId);
        if (target == null) return NotFound();

        // Admins can only be forced to reset by Super Admin
        if (target.Role == UserRole.Admin &&
            !target.IsSuperAdmin &&
            !await CurrentUserIsSuperAdminAsync())
        {
            TempData["ErrorMessage"] = "Only the Super Admin can force password resets on other admins.";
            return RedirectToAction(nameof(Index));
        }

        // Nobody can force the Super Admin to reset from this panel
        if (target.IsSuperAdmin && !IsCurrentUser(target.UserId))
        {
            TempData["ErrorMessage"] = "The Super Admin cannot be forced to reset from this panel.";
            return RedirectToAction(nameof(Index));
        }

        target.MustChangePassword = true;

        await _audit.LogAsync("user_force_password_reset", "User", target.UserId, new
        {
            email = target.Email
        });

        await _notifications.NotifyAsync(
            target.UserId,
            "Password change required",
            "An administrator has requested that you change your password. " +
            "You'll be prompted the next time you sign in.",
            NotificationType.SystemAlert);

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"{target.Name} will be asked to change their password on next login.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /{adminPrefix}/Users/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid userId)
    {
        var target = await _context.Users.FindAsync(userId);
        if (target == null) return NotFound();

        var adminId = GetAdminId();
        if (adminId == target.UserId)
        {
            TempData["ErrorMessage"] = "You can't delete your own account.";
            return RedirectToAction(nameof(Index));
        }

        // Super Admin is untouchable
        if (target.IsSuperAdmin)
        {
            TempData["ErrorMessage"] = "The Super Admin cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        // Regular admins can only be deleted by the Super Admin
        if (target.Role == UserRole.Admin && !await CurrentUserIsSuperAdminAsync())
        {
            TempData["ErrorMessage"] = "Only the Super Admin can delete other admins.";
            return RedirectToAction(nameof(Index));
        }

        target.DeletedAt = DateTime.UtcNow;
        target.IsBanned = true;
        target.BannedReason = "Account deleted";

        await _audit.LogAsync("user_deleted", "User", target.UserId, new
        {
            email = target.Email,
            role = target.Role.ToString()
        });

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"{target.Name}'s account has been deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ===================== Helpers =====================

    private Guid? GetAdminId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private bool IsCurrentUser(Guid userId) => GetAdminId() == userId;

    private async Task<bool> CurrentUserIsSuperAdminAsync()
    {
        if (GetAdminId() is not { } adminId) return false;

        return await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == adminId)
            .Select(u => u.IsSuperAdmin)
            .FirstOrDefaultAsync();
    }
}