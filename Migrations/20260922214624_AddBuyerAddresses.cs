using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kayane.Migrations
{
    /// <inheritdoc />
    public partial class AddBuyerAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "buyer_addresses",
                columns: table => new
                {
                    address_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    recipient_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    address_line_1 = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    address_line_2 = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buyer_addresses", x => x.address_id);
                    table.ForeignKey(
                        name: "FK_buyer_addresses_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_actions_action_type",
                table: "admin_actions",
                column: "action_type");

            migrationBuilder.CreateIndex(
                name: "IX_admin_actions_timestamp",
                table: "admin_actions",
                column: "timestamp",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_buyer_addresses_user_id",
                table: "buyer_addresses",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "buyer_addresses");

            migrationBuilder.DropIndex(
                name: "IX_admin_actions_action_type",
                table: "admin_actions");

            migrationBuilder.DropIndex(
                name: "IX_admin_actions_timestamp",
                table: "admin_actions");
        }
    }
}
