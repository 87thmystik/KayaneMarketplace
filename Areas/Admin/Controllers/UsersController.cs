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

        // Status filter (defaults to active only)
        switch (statusFilter)
        {
            case "banned":
                query = query.Where(u => u.IsBanned && u.DeletedAt == null);
                break;
            case "deleted":
                query = query.Where(u => u.DeletedAt != null);
                break;
            case "all":
                // no filter
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
                DeletedAt = u.DeletedAt
            })
            .ToListAsync();

        var userIds = raw.Select(r => r.UserId).ToList();

        // Order aggregates in one query
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

        return View(vm);
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

        if (target.Role == UserRole.Admin)
        {
            TempData["ErrorMessage"] = "Admins can't be banned from this panel.";
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

        if (target.Role == UserRole.Admin)
        {
            TempData["ErrorMessage"] = "Admins can't be deleted from this panel.";
            return RedirectToAction(nameof(Index));
        }

        // Soft delete
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

    private Guid? GetAdminId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}