using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kayane.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "refunded_at",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "refunded_by_admin_id",
                table: "Payments",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "refunded_at",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "refunded_by_admin_id",
                table: "Payments");
        }
    }
}
