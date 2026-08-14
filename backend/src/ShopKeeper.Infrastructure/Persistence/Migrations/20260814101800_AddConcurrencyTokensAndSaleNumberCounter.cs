using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopKeeper.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyTokensAndSaleNumberCounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RowVersion",
                table: "ProductStocks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NextSaleNumber",
                table: "BusinessSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RowVersion",
                table: "BusinessSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill NextSaleNumber from each business's existing sale count (the same
            // COUNT(*)+1 CreateSaleCommand used to compute per-request) so existing
            // businesses don't start reissuing sale numbers they've already used. This only
            // runs once, here, not per-request - the whole point of the fix is that the
            // application no longer does this at request time.
            migrationBuilder.Sql(
                """
                UPDATE "BusinessSettings" bs
                SET "NextSaleNumber" = COALESCE((SELECT COUNT(*) FROM "Sales" s WHERE s."BusinessId" = bs."BusinessId"), 0) + 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProductStocks");

            migrationBuilder.DropColumn(
                name: "NextSaleNumber",
                table: "BusinessSettings");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "BusinessSettings");
        }
    }
}
