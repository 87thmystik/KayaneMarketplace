using Microsoft.EntityFrameworkCore;
using Kayane.Models;
using Kayane.Helpers;

namespace Kayane.Data;

public class KayaneDb : DbContext
{
    public KayaneDb(DbContextOptions<KayaneDb> options) : base(options) { }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Vendor> Vendors { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<AdminAction> AdminActions { get; set; }
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();

    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderItem> OrderItems { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<PsbVirtualAccount> PsbVirtualAccounts { get; set; }
    public DbSet<VendorWallet> VendorWallets { get; set; }
    public DbSet<PayoutTransaction> PayoutTransactions { get; set; }
    public DbSet<BuyerAddress> BuyerAddresses { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ===================== PostgreSQL ENUMs =====================
        modelBuilder.HasPostgresEnum<VendorStatus>();
        modelBuilder.HasPostgresEnum<ProductStatus>();
        modelBuilder.HasPostgresEnum<OrderStatus>();
        modelBuilder.HasPostgresEnum<PaymentStatus>();
        modelBuilder.HasPostgresEnum<NotificationType>();
        modelBuilder.HasPostgresEnum<ClaimType>();

        // ===================== Relationships =====================
        modelBuilder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // ===================== Unique constraints =====================
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Vendor>()
            .HasIndex(v => v.UserId)
            .IsUnique();

        modelBuilder.Entity<Vendor>()
            .HasIndex(v => v.Slug)
            .IsUnique();

        // ===================== Performance indexes =====================

        // Orders — most queried table
        modelBuilder.Entity<Order>()
            .HasIndex(o => o.UserId)
            .HasDatabaseName("ix_orders_user_id");

        modelBuilder.Entity<Order>()
            .HasIndex(o => o.CreatedAt)
            .IsDescending()
            .HasDatabaseName("ix_orders_created_at_desc");

        modelBuilder.Entity<Order>()
            .HasIndex(o => o.PaymentStatus)
            .HasDatabaseName("ix_orders_payment_status");

        modelBuilder.Entity<Order>()
            .HasIndex(o => o.Status)
            .HasDatabaseName("ix_orders_status");

        // OrderItems — joined from Orders and Products
        modelBuilder.Entity<OrderItem>()
            .HasIndex(oi => oi.OrderId)
            .HasDatabaseName("ix_order_items_order_id");

        modelBuilder.Entity<OrderItem>()
            .HasIndex(oi => oi.ProductId)
            .HasDatabaseName("ix_order_items_product_id");

        modelBuilder.Entity<OrderItem>()
            .HasIndex(oi => oi.Status)
            .HasDatabaseName("ix_order_items_status");

        // Products — shop and vendor lists
        modelBuilder.Entity<Product>()
            .HasIndex(p => p.VendorId)
            .HasDatabaseName("ix_products_vendor_id");

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Status)
            .HasDatabaseName("ix_products_status");

        modelBuilder.Entity<Product>()
            .HasIndex(p => new { p.Status, p.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_products_status_created_at");

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.CategoryId)
            .HasDatabaseName("ix_products_category_id");

        // Notifications — inbox query
        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.IsRead })
            .HasDatabaseName("ix_notifications_user_unread");

        modelBuilder.Entity<Notification>()
            .HasIndex(n => n.CreatedAt)
            .IsDescending()
            .HasDatabaseName("ix_notifications_created_at_desc");

        // Payouts — vendor history + admin queue
        modelBuilder.Entity<PayoutTransaction>()
            .HasIndex(p => p.VendorId)
            .HasDatabaseName("ix_payout_transactions_vendor_id");

        modelBuilder.Entity<PayoutTransaction>()
            .HasIndex(p => p.Status)
            .HasDatabaseName("ix_payout_transactions_status");

        modelBuilder.Entity<PayoutTransaction>()
            .HasIndex(p => p.CreatedAt)
            .IsDescending()
            .HasDatabaseName("ix_payout_transactions_created_at_desc");

        // Admin audit log — filter + sort
        modelBuilder.Entity<AdminAction>()
            .HasIndex(a => a.Timestamp)
            .IsDescending()
            .HasDatabaseName("ix_admin_actions_timestamp_desc");

        modelBuilder.Entity<AdminAction>()
            .HasIndex(a => a.ActionType)
            .HasDatabaseName("ix_admin_actions_action_type");

        modelBuilder.Entity<AdminAction>()
            .HasIndex(a => a.TargetId)
            .HasDatabaseName("ix_admin_actions_target_id");

        // Vendor wallet lookup
        modelBuilder.Entity<VendorWallet>()
            .HasIndex(w => w.VendorId)
            .IsUnique()
            .HasDatabaseName("ix_vendor_wallets_vendor_id");

        // Payments — webhook + refund queue
        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.PaymentReference)
            .HasDatabaseName("ix_payments_reference");

        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.Status)
            .HasDatabaseName("ix_payments_status");

        // Vendors — filter + admin list
        modelBuilder.Entity<Vendor>()
            .HasIndex(v => v.Status)
            .HasDatabaseName("ix_vendors_status");

        modelBuilder.Entity<Vendor>()
            .HasIndex(v => v.CreatedAt)
            .IsDescending()
            .HasDatabaseName("ix_vendors_created_at_desc");

        // Product reviews — detail page
        modelBuilder.Entity<ProductReview>()
            .HasIndex(r => r.ProductId)
            .HasDatabaseName("ix_product_reviews_product_id");

        modelBuilder.Entity<ProductReview>()
            .HasIndex(r => new { r.ProductId, r.UserId })
            .IsUnique()
            .HasDatabaseName("ix_product_reviews_product_user");

        // Buyer addresses
        modelBuilder.Entity<BuyerAddress>()
            .HasIndex(a => a.UserId)
            .HasDatabaseName("ix_buyer_addresses_user_id");

        // Users — role filter in admin
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Role)
            .HasDatabaseName("ix_users_role");
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