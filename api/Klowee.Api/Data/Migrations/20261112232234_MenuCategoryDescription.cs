using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Klowee.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MenuCategoryDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "menu_categories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "description",
                table: "menu_categories");
        }
    }
}
