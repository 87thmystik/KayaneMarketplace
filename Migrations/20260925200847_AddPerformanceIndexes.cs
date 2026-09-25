using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kayane.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications");

            migrationBuilder.RenameIndex(
                name: "IX_VendorWallets_VendorId",
                table: "VendorWallets",
                newName: "ix_vendor_wallets_vendor_id");

            migrationBuilder.RenameIndex(
                name: "IX_Products_VendorId",
                table: "Products",
                newName: "ix_products_vendor_id");

            migrationBuilder.RenameIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                newName: "ix_products_category_id");

            migrationBuilder.RenameIndex(
                name: "IX_ProductReviews_ProductId",
                table: "ProductReviews",
                newName: "ix_product_reviews_product_id");

            migrationBuilder.RenameIndex(
                name: "IX_PayoutTransactions_VendorId",
                table: "PayoutTransactions",
                newName: "ix_payout_transactions_vendor_id");

            migrationBuilder.RenameIndex(
                name: "IX_Orders_UserId",
                table: "Orders",
                newName: "ix_orders_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                newName: "ix_order_items_product_id");

            migrationBuilder.RenameIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                newName: "ix_order_items_order_id");

            migrationBuilder.RenameIndex(
                name: "IX_buyer_addresses_user_id",
                table: "buyer_addresses",
                newName: "ix_buyer_addresses_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_admin_actions_action_type",
                table: "admin_actions",
                newName: "ix_admin_actions_action_type");

            migrationBuilder.RenameIndex(
                name: "IX_admin_actions_timestamp",
                table: "admin_actions",
                newName: "ix_admin_actions_timestamp_desc");

            migrationBuilder.CreateIndex(
                name: "ix_vendors_created_at_desc",
                table: "vendors",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_vendors_Slug",
                table: "vendors",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vendors_status",
                table: "vendors",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_role",
                table: "users",
                column: "role");

            migrationBuilder.CreateIndex(
                name: "ix_products_status",
                table: "Products",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_products_status_created_at",
                table: "Products",
                columns: new[] { "Status", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_product_reviews_product_user",
                table: "ProductReviews",
                columns: new[] { "ProductId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payout_transactions_created_at_desc",
                table: "PayoutTransactions",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_payout_transactions_status",
                table: "PayoutTransactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_payments_reference",
                table: "Payments",
                column: "PaymentReference");

            migrationBuilder.CreateIndex(
                name: "ix_payments_status",
                table: "Payments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_orders_created_at_desc",
                table: "Orders",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_orders_payment_status",
                table: "Orders",
                column: "PaymentStatus");

            migrationBuilder.CreateIndex(
                name: "ix_orders_status",
                table: "Orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_order_items_status",
                table: "OrderItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_created_at_desc",
                table: "Notifications",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_unread",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "ix_admin_actions_target_id",
                table: "admin_actions",
                column: "target_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_vendors_created_at_desc",
                table: "vendors");

            migrationBuilder.DropIndex(
                name: "IX_vendors_Slug",
                table: "vendors");

            migrationBuilder.DropIndex(
                name: "ix_vendors_status",
                table: "vendors");

            migrationBuilder.DropIndex(
                name: "IX_users_email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_role",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_products_status",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "ix_products_status_created_at",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "ix_product_reviews_product_user",
                table: "ProductReviews");

            migrationBuilder.DropIndex(
                name: "ix_payout_transactions_created_at_desc",
                table: "PayoutTransactions");

            migrationBuilder.DropIndex(
                name: "ix_payout_transactions_status",
                table: "PayoutTransactions");

            migrationBuilder.DropIndex(
                name: "ix_payments_reference",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "ix_payments_status",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "ix_orders_created_at_desc",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "ix_orders_payment_status",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "ix_orders_status",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "ix_order_items_status",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "ix_notifications_created_at_desc",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_user_unread",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "ix_admin_actions_target_id",
                table: "admin_actions");

            migrationBuilder.RenameIndex(
                name: "ix_vendor_wallets_vendor_id",
                table: "VendorWallets",
                newName: "IX_VendorWallets_VendorId");

            migrationBuilder.RenameIndex(
                name: "ix_products_vendor_id",
                table: "Products",
                newName: "IX_Products_VendorId");

            migrationBuilder.RenameIndex(
                name: "ix_products_category_id",
                table: "Products",
                newName: "IX_Products_CategoryId");

            migrationBuilder.RenameIndex(
                name: "ix_product_reviews_product_id",
                table: "ProductReviews",
                newName: "IX_ProductReviews_ProductId");

            migrationBuilder.RenameIndex(
                name: "ix_payout_transactions_vendor_id",
                table: "PayoutTransactions",
                newName: "IX_PayoutTransactions_VendorId");

            migrationBuilder.RenameIndex(
                name: "ix_orders_user_id",
                table: "Orders",
                newName: "IX_Orders_UserId");

            migrationBuilder.RenameIndex(
                name: "ix_order_items_product_id",
                table: "OrderItems",
                newName: "IX_OrderItems_ProductId");

            migrationBuilder.RenameIndex(
                name: "ix_order_items_order_id",
                table: "OrderItems",
                newName: "IX_OrderItems_OrderId");

            migrationBuilder.RenameIndex(
                name: "ix_buyer_addresses_user_id",
                table: "buyer_addresses",
                newName: "IX_buyer_addresses_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_admin_actions_action_type",
                table: "admin_actions",
                newName: "IX_admin_actions_action_type");

            migrationBuilder.RenameIndex(
                name: "ix_admin_actions_timestamp_desc",
                table: "admin_actions",
                newName: "IX_admin_actions_timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");
        }
    }
}
