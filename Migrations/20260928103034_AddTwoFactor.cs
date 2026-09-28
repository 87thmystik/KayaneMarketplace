using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kayane.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "two_factor_enabled",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "two_factor_recovery_codes",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "two_factor_secret",
                table: "users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "two_factor_enabled",
                table: "users");

            migrationBuilder.DropColumn(
                name: "two_factor_recovery_codes",
                table: "users");

            migrationBuilder.DropColumn(
                name: "two_factor_secret",
                table: "users");
        }
    }
}
