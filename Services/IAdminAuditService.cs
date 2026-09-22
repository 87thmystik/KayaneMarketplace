namespace Kayane.Services;

public interface IAdminAuditService
{
    /// <summary>
    /// Queues an audit log entry. Does NOT call SaveChanges — the caller
    /// must save so the log and the change land atomically.
    /// </summary>
    Task LogAsync(string actionType, string targetType, Guid targetId, object? details = null);
}