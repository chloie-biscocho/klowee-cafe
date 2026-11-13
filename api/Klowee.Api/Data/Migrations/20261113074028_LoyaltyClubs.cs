using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Klowee.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LoyaltyClubs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "loyalty_redemption_id",
                table: "order_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "loyalty_club_id",
                table: "menu_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "loyalty_mode",
                table: "menu_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Inherit");

            migrationBuilder.AddColumn<Guid>(
                name: "loyalty_club_id",
                table: "menu_categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "carried_over_at",
                table: "loyalty_entries",
                type: "timestamp with time zone",
                nullable: true);

            // Every existing entry belongs to the old single card, which becomes
            // The Coffee Club. The column default stays on purpose: between this
            // migration (the deploy workflow) and the new API going live on
            // Render, the old API still writes stamps without a club, and they
            // land on The Coffee Club instead of failing. The model has no
            // default, so the new API always writes club_id itself.
            migrationBuilder.AddColumn<Guid>(
                name: "club_id",
                table: "loyalty_entries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("c0ffee00-0000-4000-8000-000000000001"));

            migrationBuilder.CreateTable(
                name: "data_migrations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_migrations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "loyalty_clubs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    style = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    slot_count = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    activated_on = table.Column<DateOnly>(type: "date", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loyalty_clubs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "loyalty_club_rewards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    add_on_id = table.Column<Guid>(type: "uuid", nullable: true),
                    label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loyalty_club_rewards", x => x.id);
                    table.ForeignKey(
                        name: "fk_loyalty_club_rewards_add_ons_add_on_id",
                        column: x => x.add_on_id,
                        principalTable: "add_ons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_loyalty_club_rewards_loyalty_clubs_club_id",
                        column: x => x.club_id,
                        principalTable: "loyalty_clubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "loyalty_redemptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reward_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cycle = table.Column<int>(type: "integer", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loyalty_redemptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_loyalty_redemptions_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_loyalty_redemptions_loyalty_club_rewards_reward_id",
                        column: x => x.reward_id,
                        principalTable: "loyalty_club_rewards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_loyalty_redemptions_loyalty_clubs_club_id",
                        column: x => x.club_id,
                        principalTable: "loyalty_clubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_loyalty_redemptions_order_items_order_item_id",
                        column: x => x.order_item_id,
                        principalTable: "order_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_loyalty_redemptions_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Steps 1 and 2 of handover 16: the two clubs, with fixed ids so a
            // category copied between environments by ContentSync points at the
            // same club. Mirrored in Services/LoyaltyCarryOver.cs
            // (LoyaltyClubDefaults) for databases this migration never ran on.
            // The Matcha Club's rewards are placeholders the owners will edit.
            // "Extra shot" is looked up by name; on a database without it the
            // reward has no add-on until the owners choose one.
            migrationBuilder.Sql("""
                INSERT INTO loyalty_clubs (id, name, style, slot_count, is_active, activated_on, sort_order, created_at, updated_at)
                VALUES ('c0ffee00-0000-4000-8000-000000000001', 'The Coffee Club', 'Coffee', 12, TRUE, NULL, 1, now(), now()),
                       ('3a7c4a00-0000-4000-8000-000000000002', 'The Matcha Club', 'Matcha', 12, FALSE, NULL, 2, now(), now());

                INSERT INTO loyalty_club_rewards (club_id, slot, type, percent, add_on_id, label, created_at, updated_at)
                SELECT c.id, r.slot, r.type, r.percent,
                       CASE WHEN r.type = 'FreeAddOn' THEN
                           (SELECT a.id FROM add_ons a
                            WHERE lower(a.name) = 'extra shot' AND a.deleted_at IS NULL
                            ORDER BY a.created_at LIMIT 1)
                       END,
                       r.label, now(), now()
                FROM (VALUES ('c0ffee00-0000-4000-8000-000000000001'::uuid),
                             ('3a7c4a00-0000-4000-8000-000000000002'::uuid)) AS c(id)
                CROSS JOIN (VALUES (5, 'PercentOff', 50.00::numeric, '50% OFF'),
                                   (8, 'FreeAddOn', NULL::numeric, 'EXTRA SHOT'),
                                   (12, 'FreeDrink', NULL::numeric, 'FREE DRINK')) AS r(slot, type, percent, label);

                -- The stamps-per-reward setting is retired: the clubs carry their own rules.
                UPDATE site_settings SET deleted_at = now()
                WHERE key = 'loyalty_stamps_per_reward' AND deleted_at IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_order_items_loyalty_redemption_id",
                table: "order_items",
                column: "loyalty_redemption_id");

            migrationBuilder.CreateIndex(
                name: "ix_menu_items_loyalty_club_id",
                table: "menu_items",
                column: "loyalty_club_id");

            migrationBuilder.CreateIndex(
                name: "ix_menu_categories_loyalty_club_id",
                table: "menu_categories",
                column: "loyalty_club_id");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_entries_club_id",
                table: "loyalty_entries",
                column: "club_id");

            migrationBuilder.CreateIndex(
                name: "ix_data_migrations_name",
                table: "data_migrations",
                column: "name",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_club_rewards_add_on_id",
                table: "loyalty_club_rewards",
                column: "add_on_id");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_club_rewards_club_id_slot",
                table: "loyalty_club_rewards",
                columns: new[] { "club_id", "slot" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_clubs_name",
                table: "loyalty_clubs",
                column: "name",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_redemptions_club_id",
                table: "loyalty_redemptions",
                column: "club_id");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_redemptions_customer_id_club_id_cycle_reward_id",
                table: "loyalty_redemptions",
                columns: new[] { "customer_id", "club_id", "cycle", "reward_id" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_redemptions_order_id",
                table: "loyalty_redemptions",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_redemptions_order_item_id",
                table: "loyalty_redemptions",
                column: "order_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_redemptions_reward_id",
                table: "loyalty_redemptions",
                column: "reward_id");

            migrationBuilder.AddForeignKey(
                name: "fk_loyalty_entries_loyalty_clubs_club_id",
                table: "loyalty_entries",
                column: "club_id",
                principalTable: "loyalty_clubs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_menu_categories_loyalty_clubs_loyalty_club_id",
                table: "menu_categories",
                column: "loyalty_club_id",
                principalTable: "loyalty_clubs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_menu_items_loyalty_clubs_loyalty_club_id",
                table: "menu_items",
                column: "loyalty_club_id",
                principalTable: "loyalty_clubs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_order_items_loyalty_redemptions_loyalty_redemption_id",
                table: "order_items",
                column: "loyalty_redemption_id",
                principalTable: "loyalty_redemptions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE site_settings SET deleted_at = NULL
                WHERE key = 'loyalty_stamps_per_reward'
                  AND deleted_at = (SELECT max(deleted_at) FROM site_settings WHERE key = 'loyalty_stamps_per_reward');
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_loyalty_entries_loyalty_clubs_club_id",
                table: "loyalty_entries");

            migrationBuilder.DropForeignKey(
                name: "fk_menu_categories_loyalty_clubs_loyalty_club_id",
                table: "menu_categories");

            migrationBuilder.DropForeignKey(
                name: "fk_menu_items_loyalty_clubs_loyalty_club_id",
                table: "menu_items");

            migrationBuilder.DropForeignKey(
                name: "fk_order_items_loyalty_redemptions_loyalty_redemption_id",
                table: "order_items");

            migrationBuilder.DropTable(
                name: "data_migrations");

            migrationBuilder.DropTable(
                name: "loyalty_redemptions");

            migrationBuilder.DropTable(
                name: "loyalty_club_rewards");

            migrationBuilder.DropTable(
                name: "loyalty_clubs");

            migrationBuilder.DropIndex(
                name: "ix_order_items_loyalty_redemption_id",
                table: "order_items");

            migrationBuilder.DropIndex(
                name: "ix_menu_items_loyalty_club_id",
                table: "menu_items");

            migrationBuilder.DropIndex(
                name: "ix_menu_categories_loyalty_club_id",
                table: "menu_categories");

            migrationBuilder.DropIndex(
                name: "ix_loyalty_entries_club_id",
                table: "loyalty_entries");

            migrationBuilder.DropColumn(
                name: "loyalty_redemption_id",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "loyalty_club_id",
                table: "menu_items");

            migrationBuilder.DropColumn(
                name: "loyalty_mode",
                table: "menu_items");

            migrationBuilder.DropColumn(
                name: "loyalty_club_id",
                table: "menu_categories");

            migrationBuilder.DropColumn(
                name: "carried_over_at",
                table: "loyalty_entries");

            migrationBuilder.DropColumn(
                name: "club_id",
                table: "loyalty_entries");
        }
    }
}
