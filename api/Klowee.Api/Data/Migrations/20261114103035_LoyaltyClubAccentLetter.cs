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

            // The paper card's pink letter on the two seeded clubs (fixed ids), found
            // in the name rather than counted by hand; only while the name is as seeded.
            var coffee = "The Coffee Club";
            var matcha = "The Matcha Club";
            migrationBuilder.Sql($"UPDATE loyalty_clubs SET accent_letter = {Entities.LoyaltyClub.CoffeeAccent(coffee)} " +
                $"WHERE id = '{Entities.LoyaltyClub.CoffeeClubId}' AND lower(name) = '{coffee.ToLowerInvariant()}' AND accent_letter IS NULL;");
            migrationBuilder.Sql($"UPDATE loyalty_clubs SET accent_letter = {Entities.LoyaltyClub.MatchaAccent(matcha)} " +
                $"WHERE id = '{Entities.LoyaltyClub.MatchaClubId}' AND lower(name) = '{matcha.ToLowerInvariant()}' AND accent_letter IS NULL;");
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
