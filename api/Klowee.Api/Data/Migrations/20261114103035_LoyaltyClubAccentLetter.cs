using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Klowee.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LoyaltyClubAccentLetter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "accent_letter",
                table: "loyalty_clubs",
                type: "integer",
                nullable: true);

            // The paper card's pink letter on the two seeded clubs (fixed ids), only
            // while the name is as seeded: 13 is the U of "The Coffee Club", 5 the
            // first A of "The Matcha Club". Frozen here; LoyaltyClubDefaults finds the
            // same letters for the tests, and a test checks they are U and A.
            migrationBuilder.Sql("UPDATE loyalty_clubs SET accent_letter = 13 " +
                "WHERE id = 'c0ffee00-0000-4000-8000-000000000001' AND lower(name) = 'the coffee club' AND accent_letter IS NULL;");
            migrationBuilder.Sql("UPDATE loyalty_clubs SET accent_letter = 5 " +
                "WHERE id = '3a7c4a00-0000-4000-8000-000000000002' AND lower(name) = 'the matcha club' AND accent_letter IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "accent_letter",
                table: "loyalty_clubs");
        }
    }
}
