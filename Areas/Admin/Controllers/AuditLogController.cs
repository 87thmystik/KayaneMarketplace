using Kayane.Data;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kayane.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AuditLogController : Controller
{
    private readonly KayaneDb _context;
    private const int DefaultPageSize = 50;

    public AuditLogController(KayaneDb context)
    {
        _context = context;
    }

    // GET: /{adminPrefix}/AuditLog
    [HttpGet]
    public async Task<IActionResult> Index(
        string? actionType,
        string? targetId,
        int daysBack = 7,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = DefaultPageSize;
        if (daysBack < 1 || daysBack > 365) daysBack = 7;

        var cutoff = DateTime.UtcNow.AddDays(-daysBack);

        // Query with filter applied in SQL — no over-fetch.
        var query = _context.AdminActions
            .AsNoTracking()
            .Where(a => a.Timestamp >= cutoff);

        if (!string.IsNullOrWhiteSpace(actionType))
            query = query.Where(a => a.ActionType == actionType);

        if (!string.IsNullOrWhiteSpace(targetId) && Guid.TryParse(targetId, out var targetGuid))
            query = query.Where(a => a.TargetId == targetGuid);

        var totalItems = await query.CountAsync();

        // Page of raw rows.
        var raw = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.ActionId,
                a.Timestamp,
                a.AdminId,
                a.ActionType,
                a.TargetType,
                a.TargetId,
                a.Details
            })
            .ToListAsync();

        // Resolve admin names in a single secondary query.
        var adminIds = raw.Select(r => r.AdminId).Distinct().ToList();
        var adminInfo = await _context.Users
            .AsNoTracking()
            .Where(u => adminIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.Name, u.Email })
            .ToDictionaryAsync(u => u.UserId);

        var items = raw.Select(r =>
        {
            adminInfo.TryGetValue(r.AdminId, out var admin);
            return new AuditLogItemVM
            {
                ActionId = r.ActionId,
                Timestamp = r.Timestamp,
                AdminId = r.AdminId,
                AdminName = admin?.Name ?? "Unknown Admin",
                AdminEmail = admin?.Email ?? string.Empty,
                ActionType = r.ActionType,
                TargetType = r.TargetType,
                TargetId = r.TargetId,
                Details = r.Details
            };
        }).ToList();

        // Distinct action types for the filter dropdown — cached small list.
        var actionTypes = await _context.AdminActions
            .AsNoTracking()
            .Where(a => a.Timestamp >= DateTime.UtcNow.AddDays(-90))
            .Select(a => a.ActionType)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        var vm = new AuditLogPageVM
        {
            Items = items,
            AvailableActionTypes = actionTypes,
            SelectedActionType = actionType,
            SearchTargetId = targetId,
            DaysBack = daysBack,
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = totalItems
        };

        return View(vm);
    }
}