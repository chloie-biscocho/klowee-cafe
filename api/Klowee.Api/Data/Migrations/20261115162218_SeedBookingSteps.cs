using Klowee.Api.Services;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Klowee.Api.Data.Migrations
{
    /// <summary>
    /// Handover 20a: the Book section's steps start with the owners' approved
    /// wording. Data only, and only when no `booking_steps` row exists, so a
    /// later edit (or clearing it) is never overwritten. Down leaves the row:
    /// it may have been edited since.
    /// </summary>
    public partial class SeedBookingSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var value = BookingService.DefaultSteps.Replace("'", "''");
            migrationBuilder.Sql($"""
                INSERT INTO site_settings (key, value, created_at, updated_at)
                SELECT '{BookingService.StepsKey}', '{value}', now(), now()
                WHERE NOT EXISTS (SELECT 1 FROM site_settings WHERE key = '{BookingService.StepsKey}');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
