using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopKeeper.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleItemNetAmountPaid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NetAmountPaid",
                table: "SaleItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Backfills existing SaleItems using the same proportional-allocation formula
            // CreateSaleCommand now applies at sale time, computed from data that already
            // exists (Sale.Total, SaleItem.LineRevenue) - without this, every SaleItem created
            // before this migration would be stuck at the 0m default above, and any refund
            // against a pre-existing sale would compute a refund amount of 0.
            //
            // Pass 1: proportional share per line, ignoring the rounding remainder.
            migrationBuilder.Sql(
                """
                UPDATE "SaleItems" si
                SET "NetAmountPaid" = CASE
                    WHEN sums.line_revenue_sum = 0 THEN 0
                    ELSE ROUND(s."Total" * si."LineRevenue" / sums.line_revenue_sum, 2)
                END
                FROM "Sales" s,
                     (SELECT "SaleId", SUM("LineRevenue") AS line_revenue_sum FROM "SaleItems" GROUP BY "SaleId") sums
                WHERE si."SaleId" = s."Id" AND si."SaleId" = sums."SaleId";
                """);

            // Pass 2: adjust one line per sale (the highest-Id line, an arbitrary but
            // deterministic choice) by whatever remainder keeps that sale's lines summing
            // exactly to its Total, to the cent - the same remainder-absorption CreateSaleCommand
            // applies to the last line at sale time.
            migrationBuilder.Sql(
                """
                UPDATE "SaleItems" si
                SET "NetAmountPaid" = si."NetAmountPaid" + (s."Total" - totals.allocated_sum)
                FROM "Sales" s,
                     (SELECT "SaleId", SUM("NetAmountPaid") AS allocated_sum FROM "SaleItems" GROUP BY "SaleId") totals,
                     (SELECT DISTINCT ON ("SaleId") "SaleId", "Id" AS last_item_id FROM "SaleItems" ORDER BY "SaleId", "Id" DESC) last_items
                WHERE si."SaleId" = s."Id" AND si."SaleId" = totals."SaleId" AND si."Id" = last_items.last_item_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NetAmountPaid",
                table: "SaleItems");
        }
    }
}
