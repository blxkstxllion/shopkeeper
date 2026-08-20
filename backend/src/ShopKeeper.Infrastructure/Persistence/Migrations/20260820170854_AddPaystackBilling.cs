using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopKeeper.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaystackBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaystackCurrentPeriodEnd",
                table: "Businesses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaystackCustomerCode",
                table: "Businesses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaystackSubscriptionCode",
                table: "Businesses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaystackSubscriptionEmailToken",
                table: "Businesses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaystackSubscriptionPlanCode",
                table: "Businesses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaystackSubscriptionStatus",
                table: "Businesses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaystackWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RawPayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaystackWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaystackWebhookEvents_RawPayloadHash",
                table: "PaystackWebhookEvents",
                column: "RawPayloadHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaystackWebhookEvents");

            migrationBuilder.DropColumn(
                name: "PaystackCurrentPeriodEnd",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "PaystackCustomerCode",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "PaystackSubscriptionCode",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "PaystackSubscriptionEmailToken",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "PaystackSubscriptionPlanCode",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "PaystackSubscriptionStatus",
                table: "Businesses");
        }
    }
}
