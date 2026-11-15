using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Klowee.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BookingRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "booking_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    contact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    event_date = table.Column<DateOnly>(type: "date", nullable: false),
                    venue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    guest_count = table.Column<int>(type: "integer", nullable: true),
                    package_id = table.Column<Guid>(type: "uuid", nullable: true),
                    package_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    owner_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status_changed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_booking_requests_packages_package_id",
                        column: x => x.package_id,
                        principalTable: "packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_booking_requests_package_id",
                table: "booking_requests",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_requests_status",
                table: "booking_requests",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_requests");
        }
    }
}
