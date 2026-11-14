using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Klowee.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LoyaltyStampsAfter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "stamps_after",
                table: "loyalty_memberships",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "stamps_after",
                table: "loyalty_memberships");
        }
    }
}
