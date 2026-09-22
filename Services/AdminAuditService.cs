using Kayane.Data;
using Kayane.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Kayane.Services;

public class AdminAuditService : IAdminAuditService
{
    private readonly KayaneDb _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AdminAuditService> _logger;

    public AdminAuditService(
        KayaneDb context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AdminAuditService> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogAsync(string actionType, string targetType, Guid targetId, object? details = null)
    {
        var claim = _httpContextAccessor.HttpContext?.User?
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(claim, out var adminId))
        {
            _logger.LogWarning(
                "AdminAuditService called without a valid admin claim. Action={Action} Target={Target}/{Id}",
                actionType, targetType, targetId);
            return;
        }

        _context.AdminActions.Add(new AdminAction
        {
            ActionId = Guid.NewGuid(),
            AdminId = adminId,
            ActionType = actionType,
            TargetType = targetType,
            TargetId = targetId,
            Details = details != null
                ? JsonSerializer.Serialize(details)
                : null,
            Timestamp = DateTime.UtcNow
        });

        // Intentionally NOT calling SaveChangesAsync — see interface docs.
        await Task.CompletedTask;
    }
}