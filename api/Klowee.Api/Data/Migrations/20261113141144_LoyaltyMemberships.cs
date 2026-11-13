using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Klowee.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LoyaltyMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "auto_join_from_club_id",
                table: "loyalty_clubs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "loyalty_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    joined_on = table.Column<DateOnly>(type: "date", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loyalty_memberships", x => x.id);
                    table.ForeignKey(
                        name: "fk_loyalty_memberships_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_loyalty_memberships_loyalty_clubs_club_id",
                        column: x => x.club_id,
                        principalTable: "loyalty_clubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_clubs_auto_join_from_club_id",
                table: "loyalty_clubs",
                column: "auto_join_from_club_id");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_memberships_club_id",
                table: "loyalty_memberships",
                column: "club_id");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_memberships_customer_id_club_id",
                table: "loyalty_memberships",
                columns: new[] { "customer_id", "club_id" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_loyalty_clubs_loyalty_clubs_auto_join_from_club_id",
                table: "loyalty_clubs",
                column: "auto_join_from_club_id",
                principalTable: "loyalty_clubs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Coffee Club cardholders get the Matcha Club card automatically (handover 16a).
            migrationBuilder.Sql("""
                UPDATE loyalty_clubs SET auto_join_from_club_id = 'c0ffee00-0000-4000-8000-000000000001'
                WHERE id = '3a7c4a00-0000-4000-8000-000000000002';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_loyalty_clubs_loyalty_clubs_auto_join_from_club_id",
                table: "loyalty_clubs");

            migrationBuilder.DropTable(
                name: "loyalty_memberships");

            migrationBuilder.DropIndex(
                name: "ix_loyalty_clubs_auto_join_from_club_id",
                table: "loyalty_clubs");

            migrationBuilder.DropColumn(
                name: "auto_join_from_club_id",
                table: "loyalty_clubs");
        }
    }
}
