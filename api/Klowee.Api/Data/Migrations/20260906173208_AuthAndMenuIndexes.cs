using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Klowee.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuthAndMenuIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_site_settings_key",
                table: "site_settings");

            migrationBuilder.DropIndex(
                name: "ix_menu_version_items_menu_version_id_menu_item_id",
                table: "menu_version_items");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_site_settings_key",
                table: "site_settings",
                column: "key",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_menu_version_items_menu_version_id_menu_item_id",
                table: "menu_version_items",
                columns: new[] { "menu_version_id", "menu_item_id" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_menu_items_name",
                table: "menu_items",
                column: "name",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_menu_categories_name",
                table: "menu_categories",
                column: "name",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_add_ons_name",
                table: "add_ons",
                column: "name",
                unique: true,
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_site_settings_key",
                table: "site_settings");

            migrationBuilder.DropIndex(
                name: "ix_menu_version_items_menu_version_id_menu_item_id",
                table: "menu_version_items");

            migrationBuilder.DropIndex(
                name: "ix_menu_items_name",
                table: "menu_items");

            migrationBuilder.DropIndex(
                name: "ix_menu_categories_name",
                table: "menu_categories");

            migrationBuilder.DropIndex(
                name: "ix_add_ons_name",
                table: "add_ons");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_site_settings_key",
                table: "site_settings",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_menu_version_items_menu_version_id_menu_item_id",
                table: "menu_version_items",
                columns: new[] { "menu_version_id", "menu_item_id" },
                unique: true);
        }
    }
}
