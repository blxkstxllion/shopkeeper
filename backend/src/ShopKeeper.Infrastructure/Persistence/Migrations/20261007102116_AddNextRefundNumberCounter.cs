using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopKeeper.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNextRefundNumberCounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NextRefundNumber",
                table: "BusinessSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill from each business's existing refund count (the same COUNT(*)+1
            // RefundSaleCommand used to compute per-request) so existing businesses don't start
            // reissuing refund numbers they've already used - same approach as NextSaleNumber's
            // own backfill in AddConcurrencyTokensAndSaleNumberCounter.
            migrationBuilder.Sql(
                """
                UPDATE "BusinessSettings" bs
                SET "NextRefundNumber" = COALESCE((SELECT COUNT(*) FROM "Refunds" r WHERE r."BusinessId" = bs."BusinessId"), 0) + 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextRefundNumber",
                table: "BusinessSettings");
        }
    }
}
