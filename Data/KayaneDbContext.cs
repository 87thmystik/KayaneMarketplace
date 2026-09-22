using Microsoft.EntityFrameworkCore;
using Kayane.Models;
using Kayane.Helpers;

namespace Kayane.Data;

public class KayaneDb : DbContext
{
    // Strongly-typed options ensure correct DI resolution
    public KayaneDb(DbContextOptions<KayaneDb> options) : base(options) { }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Vendor> Vendors { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<AdminAction> AdminActions { get; set; }
    public DbSet<PayoutTransaction> PayoutTransactions { get; set; }
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();

    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<BuyerAddress> BuyerAddresses { get; set; } = null!;
    public DbSet<OrderItem> OrderItems { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<PsbVirtualAccount> PsbVirtualAccounts { get; set; }
    public DbSet<VendorWallet> VendorWallets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Map PostgreSQL ENUMs
        modelBuilder.HasPostgresEnum<VendorStatus>();
        modelBuilder.HasPostgresEnum<ProductStatus>();
        modelBuilder.HasPostgresEnum<OrderStatus>();
        modelBuilder.HasPostgresEnum<PaymentStatus>();
        modelBuilder.HasPostgresEnum<NotificationType>();
        modelBuilder.HasPostgresEnum<ClaimType>();

        // Category-Product Relationship
        modelBuilder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AdminAction>()
            .HasIndex(a => a.Timestamp)
            .IsDescending();

        modelBuilder.Entity<AdminAction>()
            .HasIndex(a => a.ActionType);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<Vendor>()
            .Where(e => e.State == EntityState.Added);

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Entity.Slug))
            {
                entry.Entity.Slug = SlugHelper.GenerateSlug(entry.Entity.BusinessName);
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}