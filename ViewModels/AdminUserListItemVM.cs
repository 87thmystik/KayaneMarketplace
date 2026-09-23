using Kayane.Models;

namespace Kayane.ViewModels;

public class AdminUserListItemVM
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }

    public bool IsBanned { get; set; }
    public string? BannedReason { get; set; }
    public bool MustChangePassword { get; set; }
    public bool IsDeleted { get; set; }

    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }

    public string Initial => string.IsNullOrWhiteSpace(Name) ? "?" : Name.Trim()[..1].ToUpperInvariant();
}

public class AdminUserListVM
{
    public List<AdminUserListItemVM> Users { get; set; } = new();

    // Filters
    public UserRole? RoleFilter { get; set; }
    public string? StatusFilter { get; set; }   // "active" | "banned" | "deleted" | null
    public string? SearchKeyword { get; set; }

    // Pagination
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalItems { get; set; }
    public int TotalPages => PageSize > 0
        ? (int)Math.Ceiling(TotalItems / (double)PageSize)
        : 1;
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;
}